namespace WorkerKit.Testing;

/// <summary>Thrown when a WorkerKit test helper check fails. The package does not reference a test framework.</summary>
public sealed class WorkerKitAssertException : Exception
{
    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">Message passed to <see cref="Exception(string)"/>.</param>
    public WorkerKitAssertException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given message and inner exception.</summary>
    /// <param name="message">Message passed to <see cref="Exception(string, Exception)"/>.</param>
    /// <param name="innerException">Exception passed to <see cref="Exception(string, Exception)"/>.</param>
    public WorkerKitAssertException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
