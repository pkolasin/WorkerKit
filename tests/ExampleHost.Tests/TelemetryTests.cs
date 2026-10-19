using System.Diagnostics;
using System.Net;
using System.Diagnostics.Metrics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Health;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Workers;
namespace ExampleHost.Tests;

public sealed class TelemetryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Resource_identity_uses_the_service_name_version_and_instance()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:ServiceName"] = " RegistryWorker ",
        }).Build();
        var identity = TelemetryIdentity.Create(configuration, "Contoso.Billing", "Production", new FakeInstance("ares"));

        Assert.Equal("RegistryWorker", identity.ServiceName);
        Assert.Equal("0.1.1", identity.ServiceVersion);
        Assert.Equal("ares", identity.InstanceName);
        Assert.Equal(Environment.MachineName, identity.HostName);
        Assert.Equal("Production", identity.EnvironmentName);
        var attributes = identity.ToSerilogAttributes();
        Assert.Equal("RegistryWorker", attributes["service.name"]);
        Assert.Equal("0.1.1", attributes["service.version"]);
        Assert.Equal("0.1.1", attributes["workerkit.version"]);
        Assert.Equal("ares", attributes["service.instance.id"]);
        Assert.Equal(Environment.MachineName, attributes["host.name"]);
        Assert.Equal("Production", attributes["deployment.environment.name"]);
    }

    [Fact]
    public void Blank_service_name_falls_back_to_the_application_name()
    {
        var configuration = new ConfigurationBuilder().Build();
        var identity = TelemetryIdentity.Create(configuration, "Contoso.Billing", "Production", new FakeInstance("default"));

        Assert.Equal("Contoso.Billing", identity.ServiceName);
    }

    [Fact]
    public void Otlp_export_stays_off_without_an_endpoint_and_reads_standard_overrides()
    {
        var empty = new ConfigurationBuilder().Build();
        Assert.False(OtlpLogExportSettings.IsEnabled(empty));
        Assert.Null(OtlpLogExportSettings.FromConfiguration(empty));
        Assert.Equal(OtlpProtocolKind.Grpc, OtlpLogExportSettings.ParseProtocol(null));
        Assert.Equal(OtlpProtocolKind.Grpc, OtlpLogExportSettings.ParseProtocol("GRPC"));
        Assert.Equal(OtlpProtocolKind.HttpProtobuf, OtlpLogExportSettings.ParseProtocol("http/protobuf"));
        Assert.Throws<InvalidOperationException>(() => OtlpLogExportSettings.ParseProtocol("http/json"));
        Assert.Throws<InvalidOperationException>(() => OtlpLogExportSettings.ParseHeaders("not-a-pair"));

        var headers = OtlpLogExportSettings.ParseHeaders("Authorization=Bearer%20x,tenant=a");
        Assert.Equal("Bearer x", headers["Authorization"]);
        Assert.Equal("a", headers["tenant"]);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = " http://127.0.0.1:4318 ",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc",
            ["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] = "http://127.0.0.1:4318/v1/logs",
            ["OTEL_EXPORTER_OTLP_LOGS_PROTOCOL"] = "http/protobuf",
            ["OTEL_EXPORTER_OTLP_HEADERS"] = "tenant=ignored",
            ["OTEL_EXPORTER_OTLP_LOGS_HEADERS"] = "tenant=logs",
        }).Build();

        var settings = OtlpLogExportSettings.FromConfiguration(configuration);
        Assert.NotNull(settings);
        Assert.Equal("http://127.0.0.1:4318", settings.Endpoint);
        Assert.Equal("http://127.0.0.1:4318/v1/logs", settings.LogsEndpoint);
        Assert.Equal(OtlpProtocolKind.HttpProtobuf, settings.Protocol);
        Assert.Equal("logs", settings.Headers["tenant"]);
    }

    [Fact]
    public void Iteration_observer_records_success_failure_and_a_cancelled_span()
    {
        var registry = new WorkerHeartbeatRegistry(new FixedHealthOptions(new HealthOptions()), new FixedTime(Now));
        registry.Register("example-poll", TimeSpan.FromMinutes(5));
        registry.Success("example-poll");
        var telemetry = new WorkerTelemetry(new TestMeterFactory(), registry);
        var observer = new TelemetryWorkerIterationObserver(telemetry);
        using var metrics = new MetricBag();
        using var activities = new ActivityCapture();

        observer.IterationStarting("example-poll");
        observer.IterationSucceeded("example-poll", TimeSpan.FromMilliseconds(250), 3);
        observer.IterationStarting("example-poll");
        observer.IterationFailed("example-poll", TimeSpan.FromSeconds(1), new InvalidOperationException("boom"));
        observer.IterationStarting("example-poll");
        observer.IterationAbandoned("example-poll");

        Assert.Equal(
            ["example-poll iteration", "example-poll iteration", "example-poll iteration"],
            activities.Names);
        Assert.Equal(ActivityStatusCode.Ok, activities.Statuses[0]);
        Assert.Equal("3", activities.ItemCounts[0]);
        Assert.Equal(ActivityStatusCode.Error, activities.Statuses[1]);
        Assert.Equal(ActivityStatusCode.Unset, activities.Statuses[2]);

        Assert.Contains(metrics.Longs, sample => sample.Name == "worker.iterations" && sample.Outcome == "success" && sample.Value == 1);
        Assert.Contains(metrics.Longs, sample => sample.Name == "worker.iterations" && sample.Outcome == "failure" && sample.Value == 1);
        Assert.Equal(2, metrics.Longs.Count(sample => sample.Name == "worker.iterations"));
        Assert.Contains(metrics.Doubles, sample => sample.Name == "worker.iteration.duration" && sample.Outcome == "success" && sample.Value == 0.25);
        Assert.Contains(metrics.Longs, sample => sample.Name == "worker.items.processed" && sample.Worker == "example-poll" && sample.Value == 3);

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["self"] = new(HealthStatus.Healthy, "process is running", TimeSpan.Zero, null, null),
                ["workers"] = new(HealthStatus.Degraded, "worker heartbeats", TimeSpan.Zero, null, null),
            },
            TimeSpan.FromMilliseconds(4));
        new MetricsHealthCheckPublisher(telemetry).PublishAsync(report, CancellationToken.None);
        metrics.Observe();

        Assert.Contains(
            metrics.Longs,
            sample => sample.Name == "worker.last_success.time"
                && sample.Worker == "example-poll"
                && sample.Value == Now.ToUnixTimeSeconds());
        Assert.Contains(metrics.Ints, sample => sample.Name == "worker.health.status" && sample.Check == "report" && sample.Value == (int)HealthStatus.Degraded);
        Assert.Contains(metrics.Ints, sample => sample.Name == "worker.health.status" && sample.Check == "self" && sample.Value == (int)HealthStatus.Healthy);
        Assert.Contains(metrics.Ints, sample => sample.Name == "worker.health.status" && sample.Check == "workers" && sample.Value == (int)HealthStatus.Degraded);
    }

    [Fact]
    public async Task Periodic_worker_notifies_the_observer_on_success_failure_and_cancellation()
    {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var heartbeat = new WorkerHeartbeatRegistry(new FixedHealthOptions(new HealthOptions()), TimeProvider.System);

        var success = new RecordingObserver();
        var succeeded = new ScriptWorker(ContextFor(scopes, heartbeat, success), (_, _) => Task.FromResult(2));
        await succeeded.StartAsync(CancellationToken.None);
        await WaitUntil(() => success.Events.Count >= 2);
        Assert.Equal(["start:script", "ok:script"], success.Events);
        await succeeded.StopAsync(CancellationToken.None);

        var failure = new RecordingObserver();
        var failed = new ScriptWorker(ContextFor(scopes, heartbeat, failure), (_, _) => throw new InvalidOperationException("nope"));
        await failed.StartAsync(CancellationToken.None);
        await WaitUntil(() => failure.Events.Count >= 2);
        Assert.Equal(["start:script", "fail:script"], failure.Events);
        await failed.StopAsync(CancellationToken.None);

        var abandoned = new RecordingObserver();
        var blocked = new ScriptWorker(ContextFor(scopes, heartbeat, abandoned), async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        });
        await blocked.StartAsync(CancellationToken.None);
        await WaitUntil(() => abandoned.Events.Count >= 1);
        Assert.Equal(["start:script"], abandoned.Events);
        await blocked.StopAsync(CancellationToken.None);
        await WaitUntil(() => abandoned.Events.Count >= 2);
        Assert.Equal(["start:script", "abandon:script"], abandoned.Events);
    }

    [Fact]
    public void Other_commands_do_not_register_telemetry_even_when_an_endpoint_is_set()
    {
        using var host = ExampleHosts.Create(
            [],
            contentRoot: Directory.CreateTempSubdirectory("ws-otel-off").FullName,
            environmentName: "Production",
            configure: builder => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:9",
            })).Build();

        Assert.Null(host.Services.GetService<TelemetryPipeline>());
        Assert.Null(host.Services.GetService<TelemetryIdentity>());
        Assert.IsType<NoOpWorkerIterationObserver>(host.Services.GetRequiredService<IWorkerIterationObserver>());
        Assert.Null(ResolveSdkService(host.Services, "OpenTelemetry.Trace.TracerProvider"));
    }

    [Fact]
    public void Run_registers_resource_attributes_and_exports_only_when_the_endpoint_is_set()
    {
        var instanceDir = Directory.CreateTempSubdirectory("ws-otel-instance").FullName;
        using var disabled = CreateRunHost(instanceDir, new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
            ["Telemetry:ServiceName"] = "RegistryWorker",
        });
        var identity = disabled.Services.GetRequiredService<TelemetryIdentity>();
        Assert.Equal("RegistryWorker", identity.ServiceName);
        Assert.Equal("1.0.0", identity.ServiceVersion);
        Assert.Equal("ares", identity.InstanceName);
        Assert.Equal(Environment.MachineName, identity.HostName);
        Assert.Equal("Production", identity.EnvironmentName);
        Assert.False(disabled.Services.GetRequiredService<TelemetryPipeline>().OtlpExporterEnabled);
        Assert.IsType<TelemetryWorkerIterationObserver>(disabled.Services.GetRequiredService<IWorkerIterationObserver>());
        Assert.NotNull(ResolveSdkService(disabled.Services, "OpenTelemetry.Trace.TracerProvider"));
        Assert.NotNull(ResolveSdkService(disabled.Services, "OpenTelemetry.Metrics.MeterProvider"));

        using var enabled = CreateRunHost(instanceDir, new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:9",
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc",
        });
        Assert.True(enabled.Services.GetRequiredService<TelemetryPipeline>().OtlpExporterEnabled);
        var logger = enabled.Services.GetRequiredService<ILoggerFactory>().CreateLogger("telemetry-test");
        logger.LogInformation("OTLP sink is configured");
    }

    [Fact]
    public async Task Run_records_an_http_client_span()
    {
        using var host = CreateRunHost(Directory.CreateTempSubdirectory("ws-otel-http").FullName, new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
            ["Worker:Enabled"] = "false",
        }, builder => builder.Services.AddHttpClient("probe"));
        var port = FreePort();
        using var server = new HttpListener();
        server.Prefixes.Add($"http://127.0.0.1:{port}/");
        server.Start();
        var serve = Task.Run(async () =>
        {
            var context = await server.GetContextAsync();
            context.Response.StatusCode = 200;
            context.Response.Close();
        });
        var stopped = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped.Add(activity.Source.Name + " " + activity.DisplayName),
        };
        ActivitySource.AddActivityListener(listener);

        await host.StartAsync();
        try
        {
            var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("probe");
            using var response = await client.GetAsync($"http://127.0.0.1:{port}/");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            await serve.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(stopped, activity => activity.Contains("Http", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.StopAsync();
            server.Close();
        }
    }

    private static IHost CreateRunHost(
        string instanceDir,
        Dictionary<string, string?> settings,
        Action<HostApplicationBuilder>? configure = null)
    {
        return ExampleHosts.Create(
            ["run", "--instance", "ares", "--instance-dir", instanceDir],
            contentRoot: Directory.CreateTempSubdirectory("ws-otel-install").FullName,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(settings);
                configure?.Invoke(builder);
            },
            enableTelemetry: true).Build();
    }

    private static async Task WaitUntil(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static object? ResolveSdkService(IServiceProvider services, string typeName)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(candidate => candidate is not null);
        type ??= Assembly.Load("OpenTelemetry").GetType(typeName);
        return type is null ? null : services.GetService(type);
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ActivityListener _listener;
        public List<string> Names { get; } = [];
        public List<ActivityStatusCode> Statuses { get; } = [];
        public List<string?> ItemCounts { get; } = [];

        public ActivityCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == WorkerTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    Names.Add(activity.DisplayName);
                    Statuses.Add(activity.Status);
                    ItemCounts.Add(activity.GetTagItem("worker.items")?.ToString());
                },
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed class MetricBag : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<LongSample> Longs { get; } = [];
        public List<DoubleSample> Doubles { get; } = [];
        public List<IntSample> Ints { get; } = [];

        public MetricBag()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == WorkerTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Longs.Add(new LongSample(instrument.Name, value, Tag(tags, "worker"), Tag(tags, "outcome"), Tag(tags, "check"))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Doubles.Add(new DoubleSample(instrument.Name, value, Tag(tags, "worker"), Tag(tags, "outcome"))));
            _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) =>
                Ints.Add(new IntSample(instrument.Name, value, Tag(tags, "check"))));
            _listener.Start();
        }

        public void Observe() => _listener.RecordObservableInstruments();

        public void Dispose() => _listener.Dispose();

        private static string? Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == key)
                {
                    return tag.Value?.ToString();
                }
            }

            return null;
        }
    }

    private sealed record LongSample(string Name, long Value, string? Worker, string? Outcome, string? Check);
    private sealed record DoubleSample(string Name, double Value, string? Worker, string? Outcome);
    private sealed record IntSample(string Name, int Value, string? Check);

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose()
        {
        }
    }

    private sealed class FixedHealthOptions(HealthOptions value) : IOptionsMonitor<HealthOptions>
    {
        public HealthOptions CurrentValue { get; } = value;
        public HealthOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<HealthOptions, string?> listener) => new Noop();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class FakeInstance(string name) : IInstanceContext
    {
        public string InstanceName { get; } = name;
        public string InstallDirectory => "/install";
        public string InstanceDirectory => "/instance";
        public string LogDirectory => "/instance/logs";
        public string DataDirectory => "/instance/data";
        public string TempDirectory => "/instance/temp";
        public string ResolvePath(string path) => path;
    }

    private sealed class RecordingObserver : IWorkerIterationObserver
    {
        public List<string> Events { get; } = [];
        public void IterationStarting(string workerName) => Events.Add("start:" + workerName);
        public void IterationSucceeded(string workerName, TimeSpan duration, int itemCount) => Events.Add("ok:" + workerName);
        public void IterationFailed(string workerName, TimeSpan duration, Exception exception) => Events.Add("fail:" + workerName);
        public void IterationAbandoned(string workerName) => Events.Add("abandon:" + workerName);
    }

    private static WorkerContext ContextFor(
        IServiceScopeFactory scopes,
        IWorkerHeartbeat heartbeat,
        IWorkerIterationObserver observer) =>
        new(scopes, heartbeat, observer, NullLoggerFactory.Instance);

    private sealed class ScriptWorker : PeriodicWorker
    {
        private readonly Func<IServiceProvider, CancellationToken, Task<int>> _run;

        public ScriptWorker(
            WorkerContext context,
            Func<IServiceProvider, CancellationToken, Task<int>> run)
            : base(context)
        {
            _run = run;
        }

        protected override string WorkerName => "script";
        protected override TimeSpan Interval => TimeSpan.FromMinutes(10);
        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            _run(services, cancellationToken);
    }

}
