namespace WorkerKit.Hosting.Cli;

public sealed class ConsoleCommandOutput : ICommandOutput
{
    public void WriteLine(string? value) => Console.WriteLine(value);

    public void WriteError(string? value) => Console.Error.WriteLine(value);
}
