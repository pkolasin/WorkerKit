using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;

namespace WorkerKit.Hosting.Tests;

public sealed class VersionHandlerTests
{
    [Fact]
    public void Execute_writes_assembly_name_and_version()
    {
        var output = new RecordingOutput();
        var handler = new VersionHandler(output);

        var code = handler.Execute();

        Assert.Equal(ExitCodes.Success, code);
        var line = Assert.Single(output.Lines);
        Assert.Contains("WorkerKit.Hosting", line, StringComparison.Ordinal);
        Assert.Contains("0.1.1", line, StringComparison.Ordinal);
    }
}
