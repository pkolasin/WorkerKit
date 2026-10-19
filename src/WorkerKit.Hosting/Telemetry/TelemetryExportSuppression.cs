using OpenTelemetry;

namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Stops the Serilog OTLP sink from creating an HTTP span for its own export calls.
/// </summary>
public static class TelemetryExportSuppression
{
    public static IDisposable Begin(bool _)
    {
        return SuppressInstrumentationScope.Begin();
    }
}
