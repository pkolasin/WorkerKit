using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Health;

using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Telemetry;

public static class WorkerDefaultsExtensions
{
    /// <summary>
    /// Registers traces, metrics, and the iteration observer for <c>run</c> and for a connector command
    /// that opts in. The OTLP exporter is added only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
    /// </summary>
    public static IHostApplicationBuilder AddWorkerDefaults(
        this IHostApplicationBuilder builder,
        IInstanceContext instance,
        WorkerAppIdentity? application = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instance);

        var identity = application is null
            ? TelemetryIdentity.Create(
                builder.Configuration,
                builder.Environment.ApplicationName,
                builder.Environment.EnvironmentName,
                instance)
            : TelemetryIdentity.Create(
                builder.Configuration,
                application,
                builder.Environment.EnvironmentName,
                instance);
        var export = OtlpLogExportSettings.IsEnabled(builder.Configuration);
        var extraSources = TelemetrySourceRegistry.Find(builder.Services)?.Names.ToArray() ?? [];
        builder.Services.AddSingleton(identity);
        builder.Services.AddSingleton(new TelemetryPipeline(export));
        builder.Services.AddSingleton<WorkerTelemetry>();
        builder.Services.Replace(ServiceDescriptor.Singleton<IWorkerIterationObserver, TelemetryWorkerIterationObserver>());
        builder.Services.AddSingleton<IHealthCheckPublisher, MetricsHealthCheckPublisher>();

        // UseOtlpExporter() in 1.20 also turns on the Microsoft.Extensions.Logging exporter.
        // Logs stay on the Serilog OTLP sink, so the protocol exporter is attached to traces and metrics only.
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    identity.ServiceName,
                    serviceVersion: identity.ServiceVersion,
                    serviceInstanceId: identity.InstanceName,
                    autoGenerateServiceInstanceId: false)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("host.name", identity.HostName),
                    new KeyValuePair<string, object>("deployment.environment.name", identity.EnvironmentName),
                ]))
            .WithTracing(tracing =>
            {
                tracing.AddSource(WorkerTelemetry.SourceName);
                if (extraSources.Length > 0)
                {
                    tracing.AddSource(extraSources);
                }

                tracing.AddHttpClientInstrumentation();
                if (export)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(WorkerTelemetry.MeterName);
                if (extraSources.Length > 0)
                {
                    metrics.AddMeter(extraSources);
                }

                metrics
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (export)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return builder;
    }
}
