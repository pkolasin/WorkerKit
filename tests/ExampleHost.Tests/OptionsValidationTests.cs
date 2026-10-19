using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting;

using WorkerKit.Samples.OpenMeteo;
namespace ExampleHost.Tests;

public sealed class OptionsValidationTests
{
    [Fact]
    public void Shipped_appsettings_are_valid()
    {
        using var host = ExampleHosts.Create(
            [],
            contentRoot: RepoPaths.HostProject(),
            environmentName: "Production").Build();

        var failures = StartupValidation.GetFailures(host.Services);

        Assert.Empty(failures);
    }

    [Fact]
    public void Development_appsettings_are_valid()
    {
        using var host = ExampleHosts.Create(
            [],
            contentRoot: RepoPaths.HostProject(),
            environmentName: "Development").Build();

        var failures = StartupValidation.GetFailures(host.Services);

        Assert.Empty(failures);
    }

    [Fact]
    public void Zero_poll_interval_fails_validation()
    {
        using var host = ExampleHosts.Create(
            [],
            contentRoot: Directory.CreateTempSubdirectory("example-options").FullName,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenMeteo:PollIntervalSeconds"] = "0",
                });
            }).Build();

        var failures = StartupValidation.GetFailures(host.Services);

        Assert.Contains(failures, failure => failure.Contains("PollIntervalSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void Command_line_overrides_the_poll_interval()
    {
        using var host = ExampleHosts.Create(
            ["run", "--OpenMeteo:PollIntervalSeconds", "12"],
            contentRoot: RepoPaths.HostProject(),
            environmentName: "Production").Build();

        var options = host.Services.GetRequiredService<IOptions<OpenMeteoOptions>>().Value;

        Assert.Equal(12, options.PollIntervalSeconds);
    }

    [Fact]
    public void Check_config_reports_invalid_options()
    {
        var output = new RecordingOutput();
        using var host = ExampleHosts.Create(
            [],
            contentRoot: Directory.CreateTempSubdirectory("example-check").FullName,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenMeteo:PollIntervalSeconds"] = "0",
                });
                builder.Services.AddSingleton<ICommandOutput>(output);
            }).Build();

        var code = host.Services.GetRequiredService<CheckConfigHandler>().Execute();

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains(output.Errors, line => line.Contains("PollIntervalSeconds", StringComparison.Ordinal));
    }
}
