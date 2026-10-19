namespace WorkerKit.Testing;

/// <summary>Exit code and console text from <see cref="WorkerAppTester.RunCommandAsync"/>.</summary>
public sealed class CommandCapture
{
    public CommandCapture(int exitCode, string standardOutput, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    public int ExitCode { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }
}
