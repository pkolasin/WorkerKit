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
/// <typeparam name="T">Payload type.</typeparam>
public interface IWorkQueue<T>
{
    /// <summary>
    /// Atomically takes up to <paramref name="max"/> items and leases each one for <paramref name="leaseDuration"/>.
    /// Two callers never receive the same item while its lease is still in the future.
    /// </summary>
    /// <param name="max">Maximum number of items to claim.</param>
    /// <param name="leaseDuration">How long each claim lasts.</param>
    /// <param name="cancellationToken">Token the caller can use to cancel the claim.</param>
    /// <returns>The items leased by this call.</returns>
    /// <remarks>A lease that expires because the process died returns the item to the queue on a later claim.</remarks>
    Task<IReadOnlyList<WorkItem<T>>> ClaimAsync(int max, TimeSpan leaseDuration, CancellationToken cancellationToken);

    /// <summary>Finishes <paramref name="item"/>. Call it only after the handler has stored its result.</summary>
    /// <param name="item">Item to complete.</param>
    /// <param name="cancellationToken">Token the caller can use to cancel completion.</param>
    /// <returns>A task that completes when <paramref name="item"/> is finished.</returns>
    Task CompleteAsync(WorkItem<T> item, CancellationToken cancellationToken);

    /// <summary>Returns <paramref name="item"/> to the queue after a transient failure or a shutdown.</summary>
    /// <param name="item">Item to return.</param>
    /// <param name="delay">Optional delay before the item can be claimed again.</param>
    /// <param name="cancellationToken">Token the caller can use to cancel the release.</param>
    /// <returns>A task that completes when <paramref name="item"/> is back on the queue.</returns>
    Task ReleaseAsync(WorkItem<T> item, TimeSpan? delay, CancellationToken cancellationToken);

    /// <summary>Quarantines <paramref name="item"/> so a later claim will not return it.</summary>
    /// <param name="item">Item that will not succeed.</param>
    /// <param name="reason">Why the item is quarantined.</param>
    /// <param name="cancellationToken">Token the caller can use to cancel the failure.</param>
    /// <returns>A task that completes when <paramref name="item"/> is quarantined.</returns>
    /// <remarks>The in-memory queue keeps quarantined items in a dead-letter list.</remarks>
    Task FailAsync(WorkItem<T> item, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Items that are not completed and not quarantined, including leased and delayed ones when the implementation counts them as waiting.
    /// Null means this queue does not report a depth. The default is null.
    /// </summary>
    /// <param name="cancellationToken">Token the caller can use to cancel the count.</param>
    /// <returns>
    /// The number of items that are not completed and not quarantined, or <see langword="null"/> when this queue does not report a depth.
    /// </returns>
    /// <remarks>The default implementation returns <c>Task.FromResult&lt;long?&gt;(null)</c>.</remarks>
    Task<long?> GetPendingCountAsync(CancellationToken cancellationToken) => Task.FromResult<long?>(null);
}
