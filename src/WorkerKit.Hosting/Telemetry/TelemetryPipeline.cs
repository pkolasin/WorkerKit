namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Whether <c>run</c> registered the OTLP exporter. Absent from the container for every other command.
/// </summary>
public sealed class TelemetryPipeline
{
    /// <summary>Stores whether this command registered the OTLP exporter.</summary>
    /// <param name="otlpExporterEnabled">Value returned by <see cref="OtlpExporterEnabled"/>.</param>
    public TelemetryPipeline(bool otlpExporterEnabled)
    {
        OtlpExporterEnabled = otlpExporterEnabled;
    }

    /// <summary>Value passed to the constructor.</summary>
    public bool OtlpExporterEnabled { get; }
}
