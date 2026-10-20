namespace WorkerKit.Abstractions.Queues;

/// <summary>
/// Connector logic for one queue payload. Register it as scoped so each item can own a database connection.
/// <see cref="HandleAsync"/> must be idempotent: the same item can be delivered more than once.
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
public interface IWorkItemHandler<T>
{
    /// <summary>Handles one leased item. The same item can be delivered more than once, so the work must be idempotent.</summary>
    /// <param name="item">Leased item to handle.</param>
    /// <param name="cancellationToken">Token the caller can use to cancel handling.</param>
    /// <returns>A task that completes when <paramref name="item"/> has been handled.</returns>
    Task HandleAsync(WorkItem<T> item, CancellationToken cancellationToken);
}
