namespace WorkerKit.Hosting.Cli;

/// <summary>Writes command text to <see cref="Console.Out"/> and <see cref="Console.Error"/>.</summary>
public sealed class ConsoleCommandOutput : ICommandOutput
{
    /// <summary>Writes <paramref name="value"/> and a line terminator to <see cref="Console.Out"/>.</summary>
    /// <param name="value">Text to write. Null writes a blank line.</param>
    public void WriteLine(string? value) => Console.WriteLine(value);

    /// <summary>Writes <paramref name="value"/> and a line terminator to <see cref="Console.Error"/>.</summary>
    /// <param name="value">Text to write. Null writes a blank line.</param>
    public void WriteError(string? value) => Console.Error.WriteLine(value);
}
