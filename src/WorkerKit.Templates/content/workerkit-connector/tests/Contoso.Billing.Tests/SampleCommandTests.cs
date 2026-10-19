using Contoso.Billing;
using WorkerKit.Hosting.Cli;
using WorkerKit.Testing;

namespace Contoso.Billing.Tests;

public sealed class SampleCommandTests
{
    [Fact]
    public async Task Sample_command_writes_the_id()
    {
        var output = new RecordingOutput();
        var command = new SampleCommand(new SampleHandler(output));

        var code = await command.ExecuteAsync(TestCommandArgs.With(SampleCommand.ItemId, " demo "), CancellationToken.None);

        Assert.Equal(0, code);
        Assert.Equal("demo", Assert.Single(output.Lines));
    }

    private sealed class RecordingOutput : ICommandOutput
    {
        public List<string?> Lines { get; } = [];

        public void WriteLine(string? value) => Lines.Add(value);

        public void WriteError(string? value) => Lines.Add(value);
    }
}
