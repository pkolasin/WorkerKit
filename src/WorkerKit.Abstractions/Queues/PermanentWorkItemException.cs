namespace WorkerKit.Abstractions.Queues;

/// <summary>
/// The item cannot succeed on another attempt. <c>QueueWorker&lt;T&gt;</c> quarantines it instead of releasing it.
/// </summary>
public sealed class PermanentWorkItemException : Exception
{
    /// <summary>Creates an exception with a message and an optional inner exception.</summary>
    /// <param name="message">Why the item cannot succeed on another attempt.</param>
    /// <param name="inner">Exception that caused this one, or <see langword="null"/>.</param>
    /// <remarks><paramref name="inner"/> defaults to <see langword="null"/>.</remarks>
    public PermanentWorkItemException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
