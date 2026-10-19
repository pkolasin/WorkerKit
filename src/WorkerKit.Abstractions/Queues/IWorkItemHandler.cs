namespace WorkerKit.Abstractions.Queues;

/// <summary>
/// Connector logic for one queue payload. Register it as scoped so each item can own a database connection.
/// <see cref="HandleAsync"/> must be idempotent: the same item can be delivered more than once.
/// </summary>
public interface IWorkItemHandler<T>
{
    Task HandleAsync(WorkItem<T> item, CancellationToken cancellationToken);
}
