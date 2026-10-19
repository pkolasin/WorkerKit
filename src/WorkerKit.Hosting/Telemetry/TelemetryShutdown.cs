using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Starts and flushes the OpenTelemetry providers for a command that does not run the generic host.
/// <see cref="OpenTelemetry.Trace.TracerProvider.Dispose"/> also flushes, but only after the provider exists,
/// and a short command returns before the hosted service would have built it.
/// </summary>
public static class TelemetryShutdown
{
    public const int FlushTimeoutMilliseconds = 10_000;

    public static void EnsureStarted(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.GetService<TracerProvider>();
        _ = services.GetService<MeterProvider>();
    }

    public static void Flush(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.GetService<TracerProvider>()?.ForceFlush(FlushTimeoutMilliseconds);
        services.GetService<MeterProvider>()?.ForceFlush(FlushTimeoutMilliseconds);
    }
}
