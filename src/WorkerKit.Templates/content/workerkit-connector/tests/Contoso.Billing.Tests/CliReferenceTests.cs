using Contoso.Billing;
using WorkerKit.Hosting.Cli;
using WorkerKit.Testing;

namespace Contoso.Billing.Tests;

public sealed class CliReferenceTests
{
    private const string Regenerate =
        "dotnet run --project src/Contoso.Billing.Host --no-launch-profile -- help --markdown > doc/cli.md";

    [Fact]
    public async Task Cli_reference_is_up_to_date()
    {
        await using var tester = WorkerAppTester.Create<SamplePollingWorker>(options => options.EnvPrefix = "CONTOSOBILLING")
            .ConfigureApp(app =>
            {
                app.Commands.Add<SampleCommand>();
                app.ConfigureHost(builder => builder.AddContosoBillingConnector());
            })
            .Build();
        var markdown = await tester.RunCommandAsync("help", "--markdown");

        Assert.Equal(ExitCodes.Success, markdown.ExitCode);
        CliReference.AssertUpToDate(FindRepoFile("doc/cli.md"), markdown.StandardOutput, Regenerate, update: false);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Contoso.Billing.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not find Contoso.Billing.sln above " + AppContext.BaseDirectory);
        }

        return Path.Combine(dir.FullName, relative);
    }
}
