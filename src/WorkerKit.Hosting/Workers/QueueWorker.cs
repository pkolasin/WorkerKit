using System.Diagnostics;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Queues;
namespace WorkerKit.Hosting.Workers;

/// <summary>
/// Claims batches from <see cref="IWorkQueue{T}"/> and runs <see cref="IWorkItemHandler{T}"/> per item.
/// Built on <see cref="PeriodicWorker"/>, so an empty queue is a successful heartbeat and a failed claim degrades readiness.
/// Each item gets its own scope and, when <c>run</c> enabled telemetry, a child span of the iteration span.
/// </summary>
public abstract class QueueWorker<T> : PeriodicWorker, IQueueBatches
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IWorkQueue<T> _queue;
    private readonly WorkerTelemetry? _telemetry;
    private TimeSpan? _idle;

    protected QueueWorker(
        WorkerContext context,
        IWorkQueue<T> queue,
        IOptionsMonitor<QueueWorkerOptions> options)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(options);
        _scopes = context.Scopes;
        _queue = queue;
        Options = options;
        _telemetry = context.Telemetry;
    }

    protected IOptionsMonitor<QueueWorkerOptions> Options { get; }

    /// <summary>
    /// Delay until the next claim. Empty batches back off from <see cref="QueueWorkerOptions.MinIdleDelay"/>
    /// up to <see cref="QueueWorkerOptions.MaxIdleDelay"/>. The first read is the max, so the heartbeat grace
    /// covers the longest idle sleep. <see cref="RunOnceAsync"/> then shrinks it when the queue has work.
    /// </summary>
    protected override TimeSpan Interval
    {
        get
        {
            var options = Options.Get(WorkerName);
            return _idle ?? options.MaxIdleDelay;
        }
    }

    /// <summary><see cref="QueueWorkerOptions.Enabled"/> for this worker. The hosted loop stops when it is false.</summary>
    protected override bool IsEnabled => Options.Get(WorkerName).Enabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            LogDisabled();
            return;
        }

        Logger.LogInformation("Queue worker {Worker} started", WorkerName);
        try
        {
            await base.ExecuteAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            Logger.LogInformation("Queue worker {Worker} stopped", WorkerName);
        }
    }

    protected override void LogDisabled()
    {
        Logger.LogInformation("Queue worker {Worker} is disabled", WorkerName);
    }

    /// <summary>
    /// One claim batch. Ignores <see cref="PeriodicWorker.IsEnabled"/>, does not sleep, and runs
    /// <see cref="PeriodicWorker.OnStartingAsync"/> only the first time for this instance.
    /// </summary>
    public Task<WorkerIterationResult> RunBatchAsync(CancellationToken cancellationToken = default) =>
        RunIterationAsync(cancellationToken);

    protected override async Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        _ = services;
        var options = Options.Get(WorkerName);
        var iteration = Activity.Current?.Context;
        var batch = await _queue.ClaimAsync(options.BatchSize, options.LeaseDuration, cancellationToken).ConfigureAwait(false);
        NoteIdle(options, batch.Count == 0);
        if (batch.Count == 0)
        {
            return 0;
        }

        // Item counts are recorded per outcome inside HandleItemAsync. Returning them here would
        // add a second worker.items.processed sample without an outcome tag.
        Exception? infrastructure = null;
        var outstanding = new Dictionary<string, WorkItem<T>>(StringComparer.Ordinal);
        foreach (var item in batch)
        {
            outstanding.Add(item.Id, item);
        }

        using var gate = new SemaphoreSlim(options.MaxConcurrency);
        var tasks = new List<Task>(batch.Count);
        foreach (var item in batch)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await HandleItemAsync(item, options, cancellationToken, iteration).ConfigureAwait(false);
                    lock (outstanding)
                    {
                        outstanding.Remove(item.Id);
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogError(exception, "Queue ack failed for item {Id}", item.Id);
                    Interlocked.CompareExchange(ref infrastructure, exception, null);
                }
                finally
                {
                    gate.Release();
                }
            }, CancellationToken.None));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            WorkItem<T>[] leftover;
            lock (outstanding)
            {
                leftover = outstanding.Values.ToArray();
                outstanding.Clear();
            }

            foreach (var item in leftover)
            {
                await _queue.ReleaseAsync(item, null, CancellationToken.None).ConfigureAwait(false);
            }
        }

        if (infrastructure is not null)
        {
            throw infrastructure;
        }

        return 0;
    }

    private async Task HandleItemAsync(
        WorkItem<T> item,
        QueueWorkerOptions options,
        CancellationToken cancellationToken,
        ActivityContext? parent)
    {
        using var activity = parent is { } context
            ? WorkerTelemetry.Source.StartActivity($"{WorkerName} item", ActivityKind.Internal, context)
            : WorkerTelemetry.Source.StartActivity($"{WorkerName} item");
        activity?.SetTag("work.item.id", item.Id);
        activity?.SetTag("work.item.attempt", item.Attempt);
        if (item.PartitionKey is not null)
        {
            activity?.SetTag("work.item.partition", item.PartitionKey);
        }

        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IWorkItemHandler<T>>();
            await handler.HandleAsync(item, cancellationToken).ConfigureAwait(false);
            await _queue.CompleteAsync(item, CancellationToken.None).ConfigureAwait(false);
            activity?.SetTag("work.item.outcome", "done");
            activity?.SetStatus(ActivityStatusCode.Ok);
            _telemetry?.RecordItem(WorkerName, "done");
        }
        catch (Exception exception) when (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("work.item.outcome", "released");
            await _queue.ReleaseAsync(item, null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is PermanentWorkItemException || item.Attempt >= options.MaxAttempts)
        {
            var reason = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
            Logger.LogError(exception, "Item {Id} failed permanently (attempt {Attempt}): {Reason}", item.Id, item.Attempt, reason);
            activity?.AddException(exception);
            activity?.SetTag("work.item.outcome", "failed");
            activity?.SetStatus(ActivityStatusCode.Error, reason);
            await _queue.FailAsync(item, reason, CancellationToken.None).ConfigureAwait(false);
            _telemetry?.RecordItem(WorkerName, "failed");
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Item {Id} failed (attempt {Attempt}), released", item.Id, item.Attempt);
            activity?.AddException(exception);
            activity?.SetTag("work.item.outcome", "retry");
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            await _queue.ReleaseAsync(item, RetryDelay(options, item.Attempt), CancellationToken.None).ConfigureAwait(false);
            _telemetry?.RecordItem(WorkerName, "retry");
        }
    }

    private void NoteIdle(QueueWorkerOptions options, bool empty)
    {
        if (!empty)
        {
            _idle = options.MinIdleDelay;
            return;
        }

        var current = _idle ?? options.MinIdleDelay;
        TimeSpan grown;
        try
        {
            grown = current > options.MaxIdleDelay / 2 ? options.MaxIdleDelay : current * 2;
        }
        catch (OverflowException)
        {
            grown = options.MaxIdleDelay;
        }

        _idle = grown > options.MaxIdleDelay ? options.MaxIdleDelay : grown;
    }

    private static TimeSpan RetryDelay(QueueWorkerOptions options, int attempt)
    {
        if (options.RetryDelay <= TimeSpan.Zero || attempt < 1)
        {
            return TimeSpan.Zero;
        }

        try
        {
            return options.RetryDelay * attempt;
        }
        catch (OverflowException)
        {
            return options.RetryDelay;
        }
    }
}
