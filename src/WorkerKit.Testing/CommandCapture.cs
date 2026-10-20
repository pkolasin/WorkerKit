namespace WorkerKit.Testing;

/// <summary>Exit code and console text from <see cref="WorkerAppTester.RunCommandAsync"/>.</summary>
public sealed class CommandCapture
{
    /// <summary>Stores the exit code and the two console texts.</summary>
    /// <param name="exitCode">Exit code to store.</param>
    /// <param name="standardOutput">Standard output to store.</param>
    /// <param name="standardError">Standard error to store.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="standardOutput"/> or <paramref name="standardError"/> is <see langword="null"/>.
    /// </exception>
    public CommandCapture(int exitCode, string standardOutput, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>Exit code stored for the command.</summary>
    public int ExitCode { get; }

    /// <summary>Standard output stored for the command.</summary>
    public string StandardOutput { get; }

    /// <summary>Standard error stored for the command.</summary>
    public string StandardError { get; }
}
