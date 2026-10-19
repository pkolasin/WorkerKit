using System.CommandLine;
using System.CommandLine.Help;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Help;
using WorkerKit.Testing;
using ExampleHost;
using ExampleHost.Composition;

namespace ExampleHost.Tests;

public sealed class CliHelpTests
{
    private const string Regenerate =
        "dotnet run --project src/ExampleHost --no-launch-profile -- help --markdown > doc/cli.md";

    [Fact]
    public void Every_command_option_and_argument_has_a_description()
    {
        var missing = new List<string>();
        Walk(CliModel.Describe(HostCommands.Create([]).Build(), ExampleHosts.Identity));
        Assert.True(missing.Count == 0, "Missing Description: " + string.Join(", ", missing));

        void Walk(CliCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Description))
            {
                missing.Add(command.Path);
            }

            missing.AddRange(command.Options
                .Where(option => string.IsNullOrWhiteSpace(option.Description))
                .Select(option => $"{command.Path} {option.Name}"));
            missing.AddRange(command.Arguments
                .Where(argument => string.IsNullOrWhiteSpace(argument.Description))
                .Select(argument => $"{command.Path} <{argument.Name}>"));
            foreach (var subcommand in command.Subcommands)
            {
                Walk(subcommand);
            }
        }
    }

    [Fact]
    public async Task Cli_reference_in_doc_is_up_to_date()
    {
        await using var tester = WorkerAppTester.Create<Program>()
            .ConfigureApp(app => app.AddExampleConnector())
            .Build();
        var markdown = await tester.RunCommandAsync("help", "--markdown");

        Assert.Equal(ExitCodes.Success, markdown.ExitCode);
        CliReference.AssertUpToDate(RepoPaths.RepoFile("doc/cli.md"), markdown.StandardOutput, Regenerate, update: false);
    }

    [Fact]
    public void Help_for_a_command_matches_built_in_help_and_uses_the_output_writer()
    {
        var viaHelp = Invoke(["help", "check-vat"]);
        var viaOption = Invoke(["check-vat", "--help"]);

        Assert.Equal(ExitCodes.Success, viaHelp.Code);
        Assert.Equal(ExitCodes.Success, viaOption.Code);
        Assert.False(string.IsNullOrWhiteSpace(viaHelp.Output));
        Assert.Equal(viaOption.Output, viaHelp.Output);
        Assert.Contains("--country-code", viaHelp.Output, StringComparison.Ordinal);
        Assert.Contains("--instance-dir", viaHelp.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_without_arguments_lists_the_tree_and_the_manual_section()
    {
        var result = Invoke(["help"]);

        Assert.Equal(ExitCodes.Success, result.Code);
        Assert.Contains("ExampleHost check-vat", result.Output, StringComparison.Ordinal);
        Assert.Contains("--country-code", result.Output, StringComparison.Ordinal);
        Assert.Contains("Aliases:", result.Output, StringComparison.Ordinal);
        Assert.Contains("Required: yes", result.Output, StringComparison.Ordinal);
        Assert.Contains("--markdown", result.Output, StringComparison.Ordinal);
        Assert.Contains("command", result.Output, StringComparison.Ordinal);
        Assert.Contains(HostConfigurationNotes.Heading, result.Output, StringComparison.Ordinal);
        Assert.Contains("--Section:Key", result.Output, StringComparison.Ordinal);
        Assert.Contains("Section__Key", result.Output, StringComparison.Ordinal);
        Assert.Contains("WORKERKITEXAMPLE_INSTANCE_DIR", result.Output, StringComparison.Ordinal);
        Assert.Contains("--instance", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_does_not_build_the_host()
    {
        var previous = Environment.GetEnvironmentVariable(InstanceLocatorName);
        Environment.SetEnvironmentVariable(InstanceLocatorName, "Not-A-Valid-Name");
        try
        {
            var result = Invoke(["help"]);
            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Contains(HostConfigurationNotes.Heading, result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InstanceLocatorName, previous);
        }
    }

    [Fact]
    public void Version_does_not_build_the_host()
    {
        var previous = Environment.GetEnvironmentVariable(InstanceLocatorName);
        Environment.SetEnvironmentVariable(InstanceLocatorName, "Not-A-Valid-Name");
        try
        {
            var result = Invoke(["version"]);
            Assert.Equal(ExitCodes.Success, result.Code);
            Assert.Contains("ExampleHost", result.Output, StringComparison.Ordinal);
            Assert.Contains("1.0.0", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InstanceLocatorName, previous);
        }
    }

    [Fact]
    public void Recursive_help_and_instance_options_are_on_the_root_only()
    {
        var root = HostCommands.Create([]).Build();
        var checkVat = root.Subcommands.Single(command => command.Name == "check-vat");
        var run = root.Subcommands.Single(command => command.Name == "run");

        Assert.Contains(root.Options, option => option is HelpOption && option.Recursive);
        Assert.Contains(root.Options, option => option.Name == "--instance" && option.Recursive);
        Assert.Contains(root.Options, option => option.Name == "--instance-dir" && option.Recursive);
        Assert.Contains(root.Options, option => option.Name == "--telemetry" && option.Recursive);
        Assert.DoesNotContain(checkVat.Options, option => option is HelpOption);
        Assert.DoesNotContain(run.Options, option => option is HelpOption);
        Assert.DoesNotContain(checkVat.Options, option => option.Name == "--instance");
        Assert.DoesNotContain(run.Options, option => option.Name == "--instance-dir");
        Assert.DoesNotContain(checkVat.Options, option => option.Name == "--telemetry");
    }

    private const string InstanceLocatorName = "WORKERKITEXAMPLE_INSTANCE";

    private static (int Code, string Output) Invoke(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = HostCommands.Create([]).Parse(args).Invoke(new InvocationConfiguration
        {
            Output = output,
            Error = error,
        });
        return (code, output.ToString());
    }
}
