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
    /// <summary>Timeout, in milliseconds, passed to <c>ForceFlush</c>. The value is 10000.</summary>
    public const int FlushTimeoutMilliseconds = 10_000;

    /// <summary>
    /// Resolves <see cref="TracerProvider"/> and <see cref="MeterProvider"/> so a short command constructs them before it returns.
    /// </summary>
    /// <param name="services">Provider that owns the OpenTelemetry registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static void EnsureStarted(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.GetService<TracerProvider>();
        _ = services.GetService<MeterProvider>();
    }

    /// <summary>
    /// Calls <c>ForceFlush</c> on the trace and meter providers when they are registered, waiting up to <see cref="FlushTimeoutMilliseconds"/>.
    /// A missing provider is skipped.
    /// </summary>
    /// <param name="services">Provider that owns the OpenTelemetry registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static void Flush(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.GetService<TracerProvider>()?.ForceFlush(FlushTimeoutMilliseconds);
        services.GetService<MeterProvider>()?.ForceFlush(FlushTimeoutMilliseconds);
    }
}
