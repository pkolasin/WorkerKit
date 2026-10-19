using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WorkerKit.Samples.Vies;

/// <summary>
/// VIES SOAP telemetry. Registered with <c>builder.AddTelemetrySource(SourceName)</c>.
/// The same name is the <see cref="ActivitySource"/> and the <see cref="Meter"/>.
/// </summary>
public static class ViesTelemetry
{
    public const string SourceName = "WorkerKit.Samples.Vies";
    public const string CallDurationName = "vies.client.duration";
    public const string CheckVatSpanName = "Vies checkVat";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);
    private static readonly Histogram<double> CallDuration = Meter.CreateHistogram<double>(
        CallDurationName,
        unit: "s",
        description: "Duration of a VIES checkVat call.");

    public static void RecordCall(TimeSpan duration, string outcome)
    {
        CallDuration.Record(duration.TotalSeconds, new TagList
        {
            { "outcome", outcome },
            { "rpc.method", "checkVat" },
        });
    }
}
