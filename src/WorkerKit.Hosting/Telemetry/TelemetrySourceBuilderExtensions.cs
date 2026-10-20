
namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Registers connector <see cref="System.Diagnostics.ActivitySource"/> and meter names for <c>AddWorkerDefaults</c>.
/// </summary>
public static class TelemetrySourceBuilderExtensions
{
    /// <summary>
    /// Adds an <see cref="System.Diagnostics.ActivitySource"/> and meter name.
    /// <c>AddWorkerDefaults</c> subscribes to it when the command enables telemetry, and exports it
    /// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set. Call this from <c>ConfigureHost</c>.
    /// </summary>
    /// <param name="builder">Host builder whose service collection stores <see cref="TelemetrySourceRegistry"/>.</param>
    /// <param name="name">Source and meter name. Leading and trailing white space is removed before it is stored.</param>
    /// <returns><paramref name="builder"/>, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public static IHostApplicationBuilder AddTelemetrySource(this IHostApplicationBuilder builder, string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        TelemetrySourceRegistry.GetOrCreate(builder.Services).Add(name.Trim());
        return builder;
    }
}
