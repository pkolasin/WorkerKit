
namespace WorkerKit.Hosting.Telemetry;

public static class TelemetrySourceBuilderExtensions
{
    /// <summary>
    /// Adds an <see cref="System.Diagnostics.ActivitySource"/> and meter name.
    /// <c>AddWorkerDefaults</c> subscribes to it when the command enables telemetry, and exports it
    /// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set. Call this from <c>ConfigureHost</c>.
    /// </summary>
    public static IHostApplicationBuilder AddTelemetrySource(this IHostApplicationBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        TelemetrySourceRegistry.GetOrCreate(builder.Services).Add(name.Trim());
        return builder;
    }
}
