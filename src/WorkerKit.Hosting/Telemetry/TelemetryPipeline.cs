namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Whether <c>run</c> registered the OTLP exporter. Absent from the container for every other command.
/// </summary>
public sealed class TelemetryPipeline
{
    public TelemetryPipeline(bool otlpExporterEnabled)
    {
        OtlpExporterEnabled = otlpExporterEnabled;
    }

    public bool OtlpExporterEnabled { get; }
}
