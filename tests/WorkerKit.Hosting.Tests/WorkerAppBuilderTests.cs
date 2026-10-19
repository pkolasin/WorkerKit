using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerKit.Abstractions.Commands;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Health;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Workers;
namespace WorkerKit.Hosting.Tests;

public sealed class WorkerAppBuilderTests
{
    [Fact]
    public async Task Help_and_version_do_not_build_the_host()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ws-app-" + Guid.NewGuid().ToString("N"));
        var help = WorkerApp.CreateBuilder<WorkerAppBuilderTests>(["help", "--instance-dir", directory]);
        help.ConfigureHost(_ => throw new InvalidOperationException("help built the host"));
        var version = WorkerApp.CreateBuilder<WorkerAppBuilderTests>(["version", "--instance-dir", directory]);
        version.ConfigureHost(_ => throw new InvalidOperationException("version built the host"));

        var helpCode = await help.RunAsync();
        var versionCode = await version.RunAsync();

        Assert.Equal(ExitCodes.Success, helpCode);
        Assert.Equal(ExitCodes.Success, versionCode);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Identity_overrides_reach_help_without_building_the_host()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ws-app-id-" + Guid.NewGuid().ToString("N"));
        var app = WorkerApp.CreateBuilder<WorkerAppBuilderTests>(
            ["help"],
            options =>
            {
                options.Name = "Contoso";
                options.ServiceName = "Contoso";
                options.EnvPrefix = "CONTOSO";
            });
        app.ConfigureHost(_ => throw new InvalidOperationException("help built the host"));
        var output = new StringWriter();
        var code = app.Commands.Parse(["help", "--instance-dir", directory]).Invoke(new InvocationConfiguration
        {
            Output = output,
            Error = new StringWriter(),
        });

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("CONTOSO_INSTANCE", output.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task ConfigureHost_runs_in_order_after_framework_services()
    {
        var seen = new List<string>();
        var app = WorkerApp.CreateBuilder<WorkerAppBuilderTests>(["marker"]);
        app.ConfigureHost(builder =>
        {
            Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IWorkerHeartbeat));
            Assert.Contains(builder.Services, descriptor => descriptor.ImplementationType == typeof(HealthEndpointWorker));
            Assert.Equal("default", builder.GetInstance().InstanceName);
            seen.Add("first");
        });
        app.ConfigureHost(_ => seen.Add("second"));
        app.Commands.Add<MarkerCommand>();
        MarkerCommand.Reset();

        var code = await app.RunAsync();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(["first", "second"], seen);
        Assert.True(MarkerCommand.Ran);
    }

    [Fact]
    public async Task Connector_command_registered_on_the_builder_runs()
    {
        MarkerCommand.Reset();
        var app = WorkerApp.CreateBuilder<WorkerAppBuilderTests>(["marker"]);
        app.Commands.Add<MarkerCommand>();

        var code = await app.RunAsync();

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(MarkerCommand.Ran);
        Assert.Equal("default", MarkerCommand.InstanceName);
        Assert.Equal("WorkerKit", MarkerCommand.ApplicationName);
    }

    [Fact]
    public async Task AddTelemetrySource_is_exported_when_the_endpoint_is_set()
    {
        SourceCommand.Reset();
        var previous = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:9");
        try
        {
            var app = WorkerApp.CreateBuilder<WorkerAppBuilderTests>(["source-probe"]);
            app.ConfigureHost(builder => builder.AddTelemetrySource(SourceCommand.SourceName));
            app.Commands.Add<SourceCommand>();

            var code = await app.RunAsync();

            Assert.Equal(ExitCodes.Success, code);
            Assert.True(SourceCommand.SourceRecorded);
            Assert.False(SourceCommand.OtherRecorded);
            Assert.True(SourceCommand.ExporterEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previous);
        }
    }

    [Fact]
    public async Task WorkerContext_worker_records_a_heartbeat()
    {
        ContextProbeWorker.Reset();
        using var host = WorkerHostBuilder.Create(
            [],
            contentRoot: Directory.CreateTempSubdirectory("ws-context").FullName,
            environmentName: "Production",
            configure: builder =>
            {
                Assert.Equal("default", builder.GetInstance().InstanceName);
                builder.Services.AddHostedService<ContextProbeWorker>();
            }).Build();

        await host.StartAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (ContextProbeWorker.Runs < 1)
            {
                await Task.Delay(20, timeout.Token);
            }
        }
        finally
        {
            await host.StopAsync();
        }

        var snapshot = host.Services.GetRequiredService<IWorkerHeartbeat>().Snapshot()
            .SingleOrDefault(worker => worker.Name == ContextProbeWorker.NameValue);
        Assert.NotNull(snapshot?.LastSuccess);
        Assert.True(ContextProbeWorker.Runs >= 1);
    }

    public sealed class MarkerCommand(IInstanceContext instance, WorkerAppIdentity identity) : IWorkerCommand
    {
        public static CommandSpec Spec { get; } = new("marker", "Marker command.");

        public static bool Ran { get; private set; }

        public static string? InstanceName { get; private set; }

        public static string? ApplicationName { get; private set; }

        public static void Reset()
        {
            Ran = false;
            InstanceName = null;
            ApplicationName = null;
        }

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        {
            Ran = true;
            InstanceName = instance.InstanceName;
            ApplicationName = identity.Name;
            return Task.FromResult(ExitCodes.Success);
        }
    }

    public sealed class SourceCommand(TelemetryPipeline pipeline) : IWorkerCommand
    {
        public const string SourceName = "tests.workerapp.connector";

        private static readonly ActivitySource Source = new(SourceName);

        private static readonly ActivitySource Other = new(SourceName + ".absent");

        public static CommandSpec Spec { get; } = new("source-probe", "Record a connector source.");

        public static bool SourceRecorded { get; private set; }

        public static bool OtherRecorded { get; private set; }

        public static bool ExporterEnabled { get; private set; }

        public static void Reset()
        {
            SourceRecorded = false;
            OtherRecorded = false;
            ExporterEnabled = false;
        }

        static bool IWorkerCommand.EnableTelemetry => true;

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        {
            using var activity = Source.StartActivity("connector");
            SourceRecorded = activity is not null;
            using var other = Other.StartActivity("other");
            OtherRecorded = other is not null;
            ExporterEnabled = pipeline.OtlpExporterEnabled;
            return Task.FromResult(ExitCodes.Success);
        }
    }

    private sealed class ContextProbeWorker : PeriodicWorker
    {
        public const string NameValue = "context-probe";

        public static int Runs { get; private set; }

        public ContextProbeWorker(WorkerContext context)
            : base(context)
        {
        }

        public static void Reset() => Runs = 0;

        protected override string WorkerName => NameValue;

        protected override TimeSpan Interval => TimeSpan.FromMilliseconds(30);

        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            Runs++;
            return Task.FromResult(1);
        }
    }
}
