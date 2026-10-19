namespace WorkerKit.Abstractions.Queues;

/// <summary>
/// Synchronous depth for the <c>worker.queue.pending</c> gauge.
/// Return null when a count would block on I/O. A database queue can cache the last <see cref="IWorkQueue{T}.GetPendingCountAsync"/> result.
/// </summary>
public interface IQueuePendingSource
{
    /// <summary>Worker or queue name used as the <c>worker</c> metric tag.</summary>
    string Name { get; }

    /// <summary>Items waiting to be claimed, or null when the count is unknown.</summary>
    long? PendingCount { get; }
}
