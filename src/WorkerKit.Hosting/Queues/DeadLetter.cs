namespace WorkerKit.Hosting.Queues;

/// <summary>
/// An item <see cref="InMemoryWorkQueue{T}"/> quarantined with <c>FailAsync</c>.
/// </summary>
public sealed record DeadLetter<T>(
    string Id,
    T Payload,
    int Attempt,
    string? PartitionKey,
    string Reason,
    DateTimeOffset FailedAt);
