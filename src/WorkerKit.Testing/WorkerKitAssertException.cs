namespace WorkerKit.Testing;

/// <summary>Thrown when a WorkerKit test helper check fails. The package does not reference a test framework.</summary>
public sealed class WorkerKitAssertException : Exception
{
    public WorkerKitAssertException(string message)
        : base(message)
    {
    }

    public WorkerKitAssertException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
