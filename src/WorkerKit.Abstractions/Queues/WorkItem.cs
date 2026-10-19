namespace WorkerKit.Abstractions.Queues;

/// <summary>
/// One leased unit of work. <see cref="Attempt"/> is 1 on the first claim and increases each time the item is claimed again.
/// <see cref="LeaseUntil"/> is the instant the lease ends. After that instant another worker may claim the same item.
/// </summary>
/// <param name="Id">Stable id inside the queue.</param>
/// <param name="Payload">Connector-specific body. Handlers must treat it as idempotent.</param>
/// <param name="Attempt">1-based count of claims, including this one.</param>
/// <param name="LeaseUntil">Exclusive end of this claim. Complete, release, or fail before another worker can reclaim.</param>
/// <param name="PartitionKey">
/// Optional ordering key, for example a customer id. A queue must not hand two live items with the same key to workers at once.
/// Null means the item may run beside any other item.
/// </param>
public sealed record WorkItem<T>(
    string Id,
    T Payload,
    int Attempt,
    DateTimeOffset LeaseUntil,
    string? PartitionKey = null);
