using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.OpenTelemetry;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting;

public static class SerilogHostBuilderExtensions
{
    /// <summary>
    /// Console sink and log levels come from the Serilog configuration section.
    /// The rolling file is attached here so a relative <c>LogFile:Path</c> is rooted at the instance directory
    /// (Windows services otherwise start in System32, and the content root is the install directory).
    /// When <paramref name="enableTelemetry"/> is set and <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is configured,
    /// the same logger also exports OTLP with the trace and metric resource attributes.
    /// </summary>
    public static void AddWorkerSerilog(
        this IHostApplicationBuilder builder,
        IInstanceContext instance,
        bool enableTelemetry,
        WorkerAppIdentity? application = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instance);
        var relativePath = builder.Configuration.GetValue(LogFileOptions.SectionName + ":Path", "logs/worker-.json")
            ?? "logs/worker-.json";
        var path = instance.ResolvePath(relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Cannot create log directory '{directory}'.", exception);
            }
        }

        var retained = builder.Configuration.GetValue(LogFileOptions.SectionName + ":RetainedFileCount", 14);
        var flushSeconds = builder.Configuration.GetValue(LogFileOptions.SectionName + ":FlushIntervalSeconds", 1);
        var hasSerilogSection = builder.Configuration.GetSection("Serilog").Exists();

        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .Enrich.WithProperty("Instance", instance.InstanceName);

            if (!hasSerilogSection)
            {
                configuration.MinimumLevel.Information();
                configuration.WriteTo.Console();
            }

            configuration.WriteTo.File(
                formatter: new RenderedCompactJsonFormatter(),
                path: path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retained,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(Math.Max(1, flushSeconds)));

            configuration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services);

            if (!enableTelemetry)
            {
                return;
            }

            var settings = OtlpLogExportSettings.FromConfiguration(builder.Configuration);
            if (settings is null)
            {
                return;
            }

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
            configuration.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = settings.Endpoint;
                if (settings.LogsEndpoint is not null)
                {
                    options.LogsEndpoint = settings.LogsEndpoint;
                }

                options.Protocol = settings.Protocol == OtlpProtocolKind.HttpProtobuf
                    ? OtlpProtocol.HttpProtobuf
                    : OtlpProtocol.Grpc;
                options.Headers = new Dictionary<string, string>(settings.Headers);
                options.ResourceAttributes = identity.ToSerilogAttributes();
                options.OnBeginSuppressInstrumentation = TelemetryExportSuppression.Begin;
            });
        });
    }
}
