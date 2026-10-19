namespace WorkerKit.Abstractions.Queues;

/// <summary>
/// At-least-once work queue. The skeleton does not reference a broker or a database queue.
/// A durable implementation belongs in its own project and is registered as this interface.
/// </summary>
/// <remarks>
/// <para><b>Claim</b> atomically takes up to <c>max</c> items and leases each one for <c>leaseDuration</c>.
/// Two callers never receive the same item while its lease is still in the future.</para>
/// <para><b>Complete</b> finishes an item. Call it only after the handler has stored its result.</para>
/// <para><b>Release</b> returns an item to the queue after a transient failure or a shutdown, optionally delaying the next claim.</para>
/// <para><b>Fail</b> quarantines an item that will not succeed. The in-memory queue keeps those items in a dead-letter list.</para>
/// <para>A lease that expires because the process died returns the item to the queue on a later claim. Delivery is at-least-once.</para>
/// <para>Order is not guaranteed. When order matters for one key, the implementation must not lease two items with that <see cref="WorkItem{T}.PartitionKey"/> at the same time.</para>
/// </remarks>
public interface IWorkQueue<T>
{
    Task<IReadOnlyList<WorkItem<T>>> ClaimAsync(int max, TimeSpan leaseDuration, CancellationToken cancellationToken);

    Task CompleteAsync(WorkItem<T> item, CancellationToken cancellationToken);

    Task ReleaseAsync(WorkItem<T> item, TimeSpan? delay, CancellationToken cancellationToken);

    Task FailAsync(WorkItem<T> item, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Items that are not completed and not quarantined, including leased and delayed ones when the implementation counts them as waiting.
    /// Null means this queue does not report a depth. The default is null.
    /// </summary>
    Task<long?> GetPendingCountAsync(CancellationToken cancellationToken) => Task.FromResult<long?>(null);
}
