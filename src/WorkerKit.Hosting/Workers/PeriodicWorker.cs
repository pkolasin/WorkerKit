using System.Diagnostics;

using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Workers;

/// <summary>
/// Shared loop for a periodic connector. Each iteration opens a scope, then records a heartbeat.
/// A failed iteration is logged and retried. Cancellation stops the loop without failing the host.
/// <see cref="IWorkerIterationObserver"/> records the iteration span and metrics when <c>run</c> enables OpenTelemetry.
/// </summary>
/// <remarks>
/// The hosted loop waits on <see cref="WorkerContext.Time"/> between iterations.
/// When <see cref="Window"/> is set, that wait is until the next occurrence instead of <see cref="Interval"/>.
/// <see cref="NoOpWorkerIterationObserver"/> records nothing when the host has not replaced it.
/// </remarks>
/// <example>
/// <code>
/// public sealed class ForecastWorker : PeriodicWorker
/// {
///     public ForecastWorker(WorkerContext context)
///         : base(context)
///     {
///     }
///
///     protected override string WorkerName => "open-meteo";
///
///     protected override TimeSpan Interval => TimeSpan.FromMinutes(15);
///
///     protected override Task&lt;int&gt; RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
///     {
///         return Task.FromResult(0);
///     }
/// }
/// </code>
/// </example>
public abstract class PeriodicWorker : BackgroundService, IWorkerIterations
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IWorkerHeartbeat _heartbeat;
    private readonly IWorkerIterationObserver _observer;
    private int _registered;
    private int _started;

    /// <summary>Stores <paramref name="context"/> and creates <see cref="Logger"/> for the concrete worker type.</summary>
    /// <param name="context">Scopes, heartbeat, observer, logger factory, telemetry, and clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    protected PeriodicWorker(WorkerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        _scopes = context.Scopes;
        _heartbeat = context.Heartbeat;
        _observer = context.Observer;
        Logger = context.CreateLogger(GetType());
    }

    /// <summary><see cref="WorkerContext"/> passed to the constructor. The hosted loop delays on <see cref="WorkerContext.Time"/>.</summary>
    protected WorkerContext Context { get; }

    /// <summary>Logger whose category is the concrete worker type.</summary>
    protected ILogger Logger { get; }

    /// <summary>Stable name used in health output, for example <c>open-meteo</c>.</summary>
    protected abstract string WorkerName { get; }

    /// <summary>Delay after each iteration when <see cref="Window"/> is null. Must be positive. Read on every iteration.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>
    /// When non-null, the hosted loop waits until <see cref="Workers.Window.NextOccurrence"/> before each iteration
    /// instead of waiting <see cref="Interval"/> after each iteration.
    /// The heartbeat interval is the gap between two occurrences when that gap is longer than <see cref="Interval"/>.
    /// <see cref="RunIterationAsync"/> does not wait.
    /// </summary>
    /// <remarks>The default is null, which keeps the interval delay. A daily window is <see cref="Workers.Window.DailyAt"/>.</remarks>
    protected virtual Window? Window => null;

    /// <summary>
    /// When false, the hosted loop logs once and returns. It does not register a heartbeat,
    /// so health stays as it is for the other workers. The default is true.
    /// <see cref="RunIterationAsync"/> ignores this switch.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="true"/>. A false value logs once through <see cref="LogDisabled"/> and skips
    /// <see cref="OnStartingAsync"/>. <see cref="RunIterationAsync"/> still runs the worker.
    /// </remarks>
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
    /// <param name="cancellationToken">Token from the hosted loop or from <see cref="RunIterationAsync"/>.</param>
    /// <returns>A completed task. A subclass can return its own startup work.</returns>
    /// <remarks>An exception propagates and clears the started flag, so the next iteration tries again. The heartbeat is not registered until this task completes.</remarks>
    protected virtual Task OnStartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// One unit of work. Return the number of items processed. An empty result is still a success.
    /// Throw to record a failed iteration. Use <paramref name="services"/> for scoped services.
    /// </summary>
    /// <param name="services">Scope created for this iteration and disposed when it finishes.</param>
    /// <param name="cancellationToken">Cancels this iteration. Cancellation is reported as <see cref="WorkerIterationStatus.Abandoned"/>.</param>
    /// <returns>The number of items processed. Zero is still a successful heartbeat.</returns>
    protected abstract Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <see cref="OnStartingAsync"/> once for this instance, registers the heartbeat on the first call,
    /// runs <see cref="RunOnceAsync"/> once, and records the outcome.
    /// Does not delay, does not wait for <see cref="Window"/>, and does not consult <see cref="IsEnabled"/>: a disabled worker still runs here.
    /// A later call does not register again, because registration resets the heartbeat, and does not run
    /// <see cref="OnStartingAsync"/> again.
    /// A non-positive <see cref="Interval"/> on the first call throws
    /// <see cref="InvalidOperationException"/> with the same message as the hosted loop.
    /// </summary>
    /// <param name="cancellationToken">Cancels <see cref="RunOnceAsync"/>. The heartbeat is not updated and the result is <see cref="WorkerIterationStatus.Abandoned"/>.</param>
    /// <returns>
    /// Succeeded with the count from <see cref="RunOnceAsync"/>, Failed with that exception, or Abandoned when <paramref name="cancellationToken"/> cancels the work.
    /// </returns>
    /// <exception cref="InvalidOperationException"><see cref="Interval"/> is zero or negative the first time this instance registers its heartbeat.</exception>
    /// <remarks>
    /// <see cref="IWorkerIterationObserver"/> is called around the work. <see cref="NoOpWorkerIterationObserver"/> records nothing.
    /// An exception from <see cref="OnStartingAsync"/> propagates, and a later call runs startup again.
    /// </remarks>
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

    /// <summary>
    /// When <see cref="IsEnabled"/> is false, calls <see cref="LogDisabled"/> once and returns without registering a heartbeat.
    /// Otherwise repeats <see cref="RunIterationAsync"/> until <paramref name="stoppingToken"/> is cancelled.
    /// </summary>
    /// <param name="stoppingToken">Stops the loop. Cancellation during an iteration abandons that iteration and ends the loop. Cancellation during the delay ends the loop.</param>
    /// <returns>A task that completes when the worker is disabled or the host stops it.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Interval"/> is zero or negative when the heartbeat is first registered.</exception>
    /// <remarks>
    /// When <see cref="Window"/> is null, the loop runs an iteration and then waits for <see cref="Interval"/> on <see cref="WorkerContext.Time"/>.
    /// A non-positive interval at that later read waits one second instead.
    /// When <see cref="Window"/> is set, the loop waits until the next occurrence before each iteration, including the first, and does not also wait for <see cref="Interval"/>.
    /// After an iteration, the following wait starts just after the occurrence that just ran, so a daily window does not run twice at the same instant.
    /// </remarks>
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
        if (Window is not null)
        {
            EnsureRegistered();
        }

        var skipCurrentOccurrence = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (Window is { } window)
            {
                var now = Context.Time.GetUtcNow();
                var due = window.NextOccurrence(skipCurrentOccurrence ? now.AddTicks(1) : now);
                var delay = due - now;
                if (delay > TimeSpan.Zero)
                {
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

            var result = await RunIterationAsync(stoppingToken).ConfigureAwait(false);
            if (result.Status == WorkerIterationStatus.Abandoned)
            {
                break;
            }

            if (Window is not null)
            {
                skipCurrentOccurrence = true;
                continue;
            }

            var idle = Interval;
            if (idle <= TimeSpan.Zero)
            {
                idle = TimeSpan.FromSeconds(1);
            }

            try
            {
                await Context.Time.Delay(idle, stoppingToken).ConfigureAwait(false);
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

        TimeSpan interval;
        try
        {
            interval = RegistrationInterval();
        }
        catch
        {
            Interlocked.Exchange(ref _registered, 0);
            throw;
        }

        _heartbeat.Register(WorkerName, interval);
    }

    private TimeSpan RegistrationInterval()
    {
        var interval = Interval;
        if (interval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Worker '{WorkerName}' interval must be positive.");
        }

        var window = Window;
        if (window is null)
        {
            return interval;
        }

        var now = Context.Time.GetUtcNow();
        var first = window.NextOccurrence(now);
        if (first <= now)
        {
            first = window.NextOccurrence(now.AddTicks(1));
        }

        var second = window.NextOccurrence(first.AddTicks(1));
        var gap = second - first;
        return gap > interval ? gap : interval;
    }
}
