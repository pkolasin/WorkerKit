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
    /// <param name="cancellationToken">Cancels the iteration.</param>
    /// <returns>The outcome of that single pass.</returns>
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
    /// <param name="cancellationToken">Cancels the batch.</param>
    /// <returns>The outcome of that claim. An empty queue is still a success.</returns>
    Task<WorkerIterationResult> RunBatchAsync(CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IWorkerIterations.RunIterationAsync"/> or <see cref="IQueueBatches.RunBatchAsync"/>.</summary>
public enum WorkerIterationStatus
{
    /// <summary><see cref="PeriodicWorker.RunOnceAsync"/> returned. The heartbeat records a success, including a count of zero.</summary>
    Succeeded,

    /// <summary><see cref="PeriodicWorker.RunOnceAsync"/> threw. The heartbeat records a failure and <see cref="WorkerIterationResult.Error"/> is that exception.</summary>
    Failed,

    /// <summary>The iteration token was cancelled during the work. The heartbeat success and failure methods are not called.</summary>
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
    /// <summary>Stores the worker name, status, item count, and error from one iteration.</summary>
    /// <param name="workerName">Worker that ran. It cannot be empty.</param>
    /// <param name="status">Outcome. It must be a defined <see cref="WorkerIterationStatus"/> value.</param>
    /// <param name="itemCount">Stored as <see cref="ItemCount"/>. This constructor does not require zero for a failed or abandoned status.</param>
    /// <param name="error">Stored as <see cref="Error"/>. Null when the iteration did not fail.</param>
    /// <exception cref="ArgumentNullException"><paramref name="workerName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workerName"/> is empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a defined <see cref="WorkerIterationStatus"/> value.</exception>
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

    /// <summary>Worker name passed to the constructor.</summary>
    public string WorkerName { get; }

    /// <summary>Outcome passed to the constructor.</summary>
    public WorkerIterationStatus Status { get; }

    /// <summary>Item count passed to the constructor.</summary>
    public int ItemCount { get; }

    /// <summary>Exception passed to the constructor, or null.</summary>
    public Exception? Error { get; }
}
