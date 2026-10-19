namespace WorkerKit.Hosting.Workers;

/// <summary>
/// One pass of a <see cref="PeriodicWorker"/>, shared by the hosted loop and tests.
/// <see cref="RunIterationAsync"/> does not sleep and ignores <see cref="PeriodicWorker.IsEnabled"/>.
/// The hosted loop honors that switch: a disabled worker logs once and does not register a heartbeat.
/// </summary>
public interface IWorkerIterations
{
    /// <summary>
    /// Runs one iteration and records the heartbeat. Does not delay and ignores
    /// <see cref="PeriodicWorker.IsEnabled"/>.
    /// </summary>
    Task<WorkerIterationResult> RunIterationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One claim batch of a <see cref="QueueWorker{T}"/>.
/// <see cref="RunBatchAsync"/> ignores <see cref="PeriodicWorker.IsEnabled"/>.
/// <see cref="PeriodicWorker.OnStartingAsync"/> runs on the first call for that instance, then not again.
/// </summary>
public interface IQueueBatches : IWorkerIterations
{
    /// <summary>One claim batch. Does not sleep and ignores <see cref="PeriodicWorker.IsEnabled"/>.</summary>
    Task<WorkerIterationResult> RunBatchAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IWorkerIterations.RunIterationAsync"/> or <see cref="IQueueBatches.RunBatchAsync"/>.</summary>
public enum WorkerIterationStatus
{
    Succeeded,
    Failed,
    Abandoned,

    /// <summary>
    /// Reserved. The hosted loop returns without a result when the worker is disabled, and
    /// <see cref="IWorkerIterations.RunIterationAsync"/> ignores the switch.
    /// </summary>
    Disabled,
}

/// <summary>Result of a single worker iteration or queue batch. A class, so the public API stays the properties below.</summary>
public sealed class WorkerIterationResult
{
    public WorkerIterationResult(string workerName, WorkerIterationStatus status, int itemCount, Exception? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        WorkerName = workerName;
        Status = status;
        ItemCount = itemCount;
        Error = error;
    }

    public string WorkerName { get; }

    public WorkerIterationStatus Status { get; }

    public int ItemCount { get; }

    public Exception? Error { get; }
}
