namespace WorkerKit.Hosting.Cli;

/// <summary>Process exit codes returned by built-in commands.</summary>
public static class ExitCodes
{
    /// <summary>The command finished without a reported error. The value is 0.</summary>
    public const int Success = 0;

    /// <summary>The command reported an error. The value is 1.</summary>
    public const int Error = 1;
}
