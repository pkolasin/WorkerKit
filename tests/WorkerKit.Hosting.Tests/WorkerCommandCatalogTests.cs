using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using WorkerKit.Abstractions.Commands;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting.Tests;

public sealed class WorkerCommandCatalogTests
{
    private static string InstanceVariable => AppIdentity.Current.InstanceVariable;

    [Fact]
    public void A_root_name_that_matches_a_built_in_command_fails_at_startup()
    {
        var command = Assert.Throws<InvalidOperationException>(() => WorkerCommands.Create([]).Add<RunCollisionCommand>());
        var group = Assert.Throws<InvalidOperationException>(() =>
            WorkerCommands.Create([]).AddGroup("help", "Not a group.", _ => { }));

        Assert.Contains("run", command.Message, StringComparison.Ordinal);
        Assert.Contains("built-in", command.Message, StringComparison.Ordinal);
        Assert.Contains("help", group.Message, StringComparison.Ordinal);
        Assert.Contains("built-in", group.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_duplicate_command_name_fails_at_startup()
    {
        var commands = WorkerCommands.Create([]);
        commands.Add<ProbeCommand>();

        var duplicate = Assert.Throws<InvalidOperationException>(() => commands.Add<ProbeCommand>());
        var grouped = Assert.Throws<InvalidOperationException>(() =>
            WorkerCommands.Create([]).AddGroup("registry", "Registry commands.", group =>
                group.Add<ProbeCommand>().Add<ProbeCommand>()));

        Assert.Contains("probe", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("already registered", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains("probe", grouped.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_includes_a_connector_command_without_building_the_host()
    {
        var previous = Environment.GetEnvironmentVariable(InstanceVariable);
        Environment.SetEnvironmentVariable(InstanceVariable, "Not-A-Valid-Name");
        try
        {
            var commands = WorkerCommands.Create([]);
            commands.AddGroup("registry", "Registry commands.", group => group.Add<ProbeCommand>());
            var output = new StringWriter();
            var code = commands.Parse(["help"]).Invoke(new InvocationConfiguration
            {
                Output = output,
                Error = new StringWriter(),
            });
            var markdown = new StringWriter();
            var markdownCode = commands.Parse(["help", "--markdown"]).Invoke(new InvocationConfiguration
            {
                Output = markdown,
                Error = new StringWriter(),
            });

            Assert.Equal(ExitCodes.Success, code);
            Assert.Equal(ExitCodes.Success, markdownCode);
            Assert.Contains("WorkerKit.Hosting registry probe", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("--name", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Registry commands.", output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("--source", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("registry probe", markdown.ToString(), StringComparison.Ordinal);
            Assert.Contains("--name", markdown.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InstanceVariable, previous);
        }
    }

    [Fact]
    public async Task A_connector_command_runs_in_a_scope_without_telemetry_and_defaults_the_instance()
    {
        ProbeCommand.Reset();
        var args = new[] { "probe", "--name", "ada" };
        var commands = WorkerCommands.Create(args);
        commands.Add<ProbeCommand>();

        var code = await commands.Parse(args).InvokeAsync();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal("ada", ProbeCommand.SeenName);
        Assert.Equal("default", ProbeCommand.SeenInstance);
        Assert.False(ProbeCommand.TelemetryRegistered);
        Assert.False(ProbeCommand.ActivityCreated);
        Assert.Null(WorkerTelemetry.Source.StartActivity("after-default"));
    }

    [Fact]
    public async Task Telemetry_override_records_a_span_and_disposes_the_pipeline()
    {
        TelemetryProbeCommand.Reset();
        var args = new[] { "telemetry-probe" };
        var commands = WorkerCommands.Create(args);
        commands.Add<TelemetryProbeCommand>();

        var code = await commands.Parse(args).InvokeAsync();

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(TelemetryProbeCommand.TelemetryRegistered);
        Assert.True(TelemetryProbeCommand.ActivityCreated);
        Assert.False(TelemetryProbeCommand.ExporterEnabled);
        Assert.Null(WorkerTelemetry.Source.StartActivity("after-override"));
    }

    [Fact]
    public async Task Telemetry_flag_enables_a_command_that_does_not_opt_in()
    {
        ProbeCommand.Reset();
        var args = new[] { "probe", "--name", "ada", "--telemetry" };
        var commands = WorkerCommands.Create(args);
        commands.Add<ProbeCommand>();

        var code = await commands.Parse(args).InvokeAsync();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal("ada", ProbeCommand.SeenName);
        Assert.True(ProbeCommand.TelemetryRegistered);
        Assert.True(ProbeCommand.ActivityCreated);
        Assert.False(ProbeCommand.ExporterEnabled);
        Assert.Null(WorkerTelemetry.Source.StartActivity("after-flag"));
    }

    [Fact]
    public async Task Telemetry_exports_only_when_the_endpoint_is_set()
    {
        TelemetryProbeCommand.Reset();
        var previous = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:9");
        try
        {
            var args = new[] { "telemetry-probe" };
            var commands = WorkerCommands.Create(args);
            commands.Add<TelemetryProbeCommand>();

            var code = await commands.Parse(args).InvokeAsync();

            Assert.Equal(ExitCodes.Success, code);
            Assert.True(TelemetryProbeCommand.ExporterEnabled);
            Assert.True(TelemetryProbeCommand.ActivityCreated);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previous);
        }
    }

    [Fact]
    public void Help_and_version_ignore_telemetry_and_do_not_build_the_host()
    {
        var previous = Environment.GetEnvironmentVariable(InstanceVariable);
        Environment.SetEnvironmentVariable(InstanceVariable, "Not-A-Valid-Name");
        try
        {
            var help = Invoke(["help", "--telemetry"]);
            var version = Invoke(["version", "--telemetry"]);

            Assert.Equal(ExitCodes.Success, help.Code);
            Assert.Contains("--telemetry", help.Output, StringComparison.Ordinal);
            Assert.Equal(ExitCodes.Success, version.Code);
            Assert.Contains("WorkerKit.Hosting", version.Output, StringComparison.Ordinal);
            Assert.Contains("0.1.1", version.Output, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InstanceVariable, previous);
        }
    }

    [Fact]
    public void Check_config_accepts_telemetry_and_still_builds_the_host()
    {
        var previous = Environment.GetEnvironmentVariable(InstanceVariable);
        Environment.SetEnvironmentVariable(InstanceVariable, "Not-A-Valid-Name");
        try
        {
            var parsed = WorkerCommands.Create(["check-config", "--telemetry"]).Parse(["check-config", "--telemetry"]);
            Assert.Empty(parsed.Errors);

            var code = parsed.Invoke();
            Assert.Equal(ExitCodes.Error, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InstanceVariable, previous);
        }
    }

    [Fact]
    public async Task Invalid_options_stop_a_connector_command_before_execute()
    {
        ProbeCommand.Reset();
        var previous = Environment.GetEnvironmentVariable("Health__PublisherPeriodSeconds");
        Environment.SetEnvironmentVariable("Health__PublisherPeriodSeconds", "0");
        try
        {
            var commands = WorkerCommands.Create(["probe", "--name", "ada"]);
            commands.Add<ProbeCommand>();

            var code = await commands.Parse(["probe", "--name", "ada"]).InvokeAsync();

            Assert.Equal(ExitCodes.Error, code);
            Assert.Null(ProbeCommand.SeenName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Health__PublisherPeriodSeconds", previous);
        }
    }

    [Fact]
    public async Task ValidateOptions_false_runs_when_configuration_is_invalid()
    {
        SkipValidationCommand.Reset();
        var previous = Environment.GetEnvironmentVariable("Health__PublisherPeriodSeconds");
        Environment.SetEnvironmentVariable("Health__PublisherPeriodSeconds", "0");
        try
        {
            var commands = WorkerCommands.Create(["skip-validation"]);
            commands.Add<SkipValidationCommand>();

            var code = await commands.Parse(["skip-validation"]).InvokeAsync();

            Assert.Equal(ExitCodes.Success, code);
            Assert.True(SkipValidationCommand.Executed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Health__PublisherPeriodSeconds", previous);
        }
    }

    public sealed class ProbeCommand : IWorkerCommand
    {
        public static readonly CommandOption<string> NameOption = new("--name", "Value to print.")
        {
            Required = true,
        };

        public static CommandSpec Spec { get; } = new("probe", "Print one value.") { NameOption };

        private readonly IInstanceContext _instance;
        private readonly ICommandOutput _output;
        private readonly IServiceProvider _services;

        public ProbeCommand(IInstanceContext instance, ICommandOutput output, IServiceProvider services)
        {
            _instance = instance;
            _output = output;
            _services = services;
        }

        public static string? SeenName { get; private set; }

        public static string? SeenInstance { get; private set; }

        public static bool TelemetryRegistered { get; private set; }

        public static bool ActivityCreated { get; private set; }

        public static bool ExporterEnabled { get; private set; }

        public static void Reset()
        {
            SeenName = null;
            SeenInstance = null;
            TelemetryRegistered = false;
            ActivityCreated = false;
            ExporterEnabled = false;
        }

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        {
            SeenName = args.Get(NameOption);
            SeenInstance = _instance.InstanceName;
            TelemetryRegistered = _services.GetService<WorkerTelemetry>() is not null;
            ExporterEnabled = _services.GetService<TelemetryPipeline>()?.OtlpExporterEnabled == true;
            using var activity = WorkerTelemetry.Source.StartActivity("connector command");
            ActivityCreated = activity is not null;
            _output.WriteLine(SeenName);
            return Task.FromResult(ExitCodes.Success);
        }
    }

    public sealed class RunCollisionCommand : IWorkerCommand
    {
        public static CommandSpec Spec { get; } = new("run", "Collides with the built-in run command.");

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken) =>
            Task.FromResult(ExitCodes.Success);
    }

    public sealed class SkipValidationCommand : IWorkerCommand
    {
        public static bool Executed { get; private set; }

        public static CommandSpec Spec { get; } = new("skip-validation", "Run without options validation.");

        public static void Reset() => Executed = false;

        static bool IWorkerCommand.ValidateOptions => false;

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        {
            Executed = true;
            return Task.FromResult(ExitCodes.Success);
        }
    }

    public sealed class TelemetryProbeCommand : IWorkerCommand
    {
        public static bool TelemetryRegistered { get; private set; }

        public static bool ActivityCreated { get; private set; }

        public static bool ExporterEnabled { get; private set; }

        public static CommandSpec Spec { get; } = new("telemetry-probe", "Record one span.");

        private readonly IServiceProvider _services;

        public TelemetryProbeCommand(IServiceProvider services)
        {
            _services = services;
        }

        public static void Reset()
        {
            TelemetryRegistered = false;
            ActivityCreated = false;
            ExporterEnabled = false;
        }

        static bool IWorkerCommand.EnableTelemetry => true;

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        {
            TelemetryRegistered = _services.GetService<WorkerTelemetry>() is not null;
            ExporterEnabled = _services.GetRequiredService<TelemetryPipeline>().OtlpExporterEnabled;
            using var activity = WorkerTelemetry.Source.StartActivity("connector command");
            ActivityCreated = activity is not null;
            return Task.FromResult(ExitCodes.Success);
        }
    }

    private static (int Code, string Output) Invoke(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = WorkerCommands.Create(args).Parse(args).Invoke(new InvocationConfiguration
        {
            Output = output,
            Error = error,
        });
        return (code, output.ToString());
    }
}
