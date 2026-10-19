using System.Diagnostics;

using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Workers;

/// <summary>
/// Shared loop for a periodic connector. Each iteration opens a scope, then records a heartbeat.
/// A failed iteration is logged and retried. Cancellation stops the loop without failing the host.
/// <see cref="IWorkerIterationObserver"/> records the iteration span and metrics when <c>run</c> enables OpenTelemetry.
/// </summary>
public abstract class PeriodicWorker : BackgroundService, IWorkerIterations
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IWorkerHeartbeat _heartbeat;
    private readonly IWorkerIterationObserver _observer;
    private int _registered;
    private int _started;

    protected PeriodicWorker(WorkerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        _scopes = context.Scopes;
        _heartbeat = context.Heartbeat;
        _observer = context.Observer;
        Logger = context.CreateLogger(GetType());
    }

    protected WorkerContext Context { get; }

    protected ILogger Logger { get; }

    /// <summary>Stable name used in health output, for example <c>open-meteo</c>.</summary>
    protected abstract string WorkerName { get; }

    /// <summary>Delay after each iteration. Must be positive. Read on every iteration.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>
    /// When false, the hosted loop logs once and returns. It does not register a heartbeat,
    /// so health stays as it is for the other workers. The default is true.
    /// <see cref="RunIterationAsync"/> ignores this switch.
    /// </summary>
    protected virtual bool IsEnabled => true;

    /// <summary>Called once when <see cref="IsEnabled"/> is false. The hosted loop does not call it again.</summary>
    protected virtual void LogDisabled()
    {
        Logger.LogInformation("Worker {Worker} is disabled", WorkerName);
    }

    /// <summary>
    /// Runs once before the first iteration of this instance, from the hosted loop or from
    /// <see cref="RunIterationAsync"/>. A later call does not run it again.
    /// The hosted loop skips it when <see cref="IsEnabled"/> is false.
    /// </summary>
    protected virtual Task OnStartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// One unit of work. Return the number of items processed. An empty result is still a success.
    /// Throw to record a failed iteration. Use <paramref name="services"/> for scoped services.
    /// </summary>
    protected abstract Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <see cref="OnStartingAsync"/> once for this instance, registers the heartbeat on the first call,
    /// runs <see cref="RunOnceAsync"/> once, and records the outcome.
    /// Does not delay, and does not consult <see cref="IsEnabled"/>: a disabled worker still runs here.
    /// A later call does not register again, because registration resets the heartbeat, and does not run
    /// <see cref="OnStartingAsync"/> again.
    /// A non-positive <see cref="Interval"/> on the first call throws
    /// <see cref="InvalidOperationException"/> with the same message as the hosted loop.
    /// </summary>
    public async Task<WorkerIterationResult> RunIterationAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        EnsureRegistered();
        var started = Stopwatch.GetTimestamp();
        _observer.IterationStarting(WorkerName);
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var items = await RunOnceAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
            _observer.IterationSucceeded(WorkerName, Stopwatch.GetElapsedTime(started), items);
            _heartbeat.Success(WorkerName);
            return new WorkerIterationResult(WorkerName, WorkerIterationStatus.Succeeded, items, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _observer.IterationAbandoned(WorkerName);
            return new WorkerIterationResult(WorkerName, WorkerIterationStatus.Abandoned, 0, null);
        }
        catch (Exception exception)
        {
            _observer.IterationFailed(WorkerName, Stopwatch.GetElapsedTime(started), exception);
            _heartbeat.Failure(WorkerName, exception);
            Logger.LogError(exception, "Worker {Worker} iteration failed and will be retried", WorkerName);
            return new WorkerIterationResult(WorkerName, WorkerIterationStatus.Failed, 0, exception);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            LogDisabled();
            return;
        }

        await RunLoopAsync(stoppingToken).ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await RunIterationAsync(stoppingToken).ConfigureAwait(false);
            if (result.Status == WorkerIterationStatus.Abandoned)
            {
                break;
            }

            var delay = Interval;
            if (delay <= TimeSpan.Zero)
            {
                delay = TimeSpan.FromSeconds(1);
            }

            try
            {
                await Context.Time.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await OnStartingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Exchange(ref _started, 0);
            throw;
        }
    }

    private void EnsureRegistered()
    {
        if (Interlocked.CompareExchange(ref _registered, 1, 0) != 0)
        {
            return;
        }

        var interval = Interval;
        if (interval <= TimeSpan.Zero)
        {
            Interlocked.Exchange(ref _registered, 0);
            throw new InvalidOperationException($"Worker '{WorkerName}' interval must be positive.");
        }

        _heartbeat.Register(WorkerName, interval);
    }
}
