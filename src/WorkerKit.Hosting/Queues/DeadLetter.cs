namespace WorkerKit.Hosting.Queues;

/// <summary>
/// An item <see cref="InMemoryWorkQueue{T}"/> quarantined with <c>FailAsync</c>.
/// </summary>
/// <typeparam name="T">Payload stored on the work item.</typeparam>
/// <param name="Id">Identifier of the quarantined item.</param>
/// <param name="Payload">Payload copied from the queue entry.</param>
/// <param name="Attempt">Claim count at the time of the failure.</param>
/// <param name="PartitionKey">Partition key from the queue entry, or null.</param>
/// <param name="Reason">Text passed to <c>FailAsync</c>.</param>
/// <param name="FailedAt">Clock time when the item was quarantined.</param>
public sealed record DeadLetter<T>(
    string Id,
    T Payload,
    int Attempt,
    string? PartitionKey,
    string Reason,
    DateTimeOffset FailedAt);
