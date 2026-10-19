namespace WorkerKit.Abstractions.Queues;

/// <summary>
/// The item cannot succeed on another attempt. <c>QueueWorker&lt;T&gt;</c> quarantines it instead of releasing it.
/// </summary>
public sealed class PermanentWorkItemException : Exception
{
    public PermanentWorkItemException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
