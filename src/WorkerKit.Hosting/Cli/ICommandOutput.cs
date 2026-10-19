namespace WorkerKit.Hosting.Cli;

public interface ICommandOutput
{
    void WriteLine(string? value);

    void WriteError(string? value);
}
