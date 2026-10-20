namespace WorkerKit.Hosting.Cli;

/// <summary>Destination for command text and errors.</summary>
public interface ICommandOutput
{
    /// <summary>Writes <paramref name="value"/> as a line of normal output.</summary>
    /// <param name="value">Text to write.</param>
    void WriteLine(string? value);

    /// <summary>Writes <paramref name="value"/> as a line of error output.</summary>
    /// <param name="value">Text to write.</param>
    void WriteError(string? value);
}
