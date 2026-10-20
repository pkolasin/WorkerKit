using OpenTelemetry;

namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Stops the Serilog OTLP sink from creating an HTTP span for its own export calls.
/// </summary>
public static class TelemetryExportSuppression
{
    /// <summary>
    /// Enters <see cref="SuppressInstrumentationScope"/>. The argument is ignored, so the scope always suppresses instrumentation.
    /// </summary>
    /// <param name="_">Ignored.</param>
    /// <returns>The scope returned by <see cref="SuppressInstrumentationScope"/>. Disposing it ends suppression.</returns>
    public static IDisposable Begin(bool _)
    {
        return SuppressInstrumentationScope.Begin();
    }
}
