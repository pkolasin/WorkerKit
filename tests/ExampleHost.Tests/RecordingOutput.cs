using WorkerKit.Hosting.Cli;

namespace ExampleHost.Tests;

internal sealed class RecordingOutput : ICommandOutput
{
    public List<string> Lines { get; } = [];

    public List<string> Errors { get; } = [];

    public void WriteLine(string? value) => Lines.Add(value ?? "");

    public void WriteError(string? value) => Errors.Add(value ?? "");
}
