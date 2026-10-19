using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkerKit.Hosting.Telemetry;
using WorkerKit.Samples.Vies;

namespace WorkerKit.Samples.Tests;

public sealed class ViesChannelTests
{
    [Fact]
    public async Task Failed_check_is_a_child_span_and_records_a_failure_duration()
    {
        var stopped = new List<(string Name, ActivityStatusCode Status, ActivitySpanId Parent)>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is WorkerTelemetry.SourceName or ViesTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped.Add((activity.DisplayName, activity.Status, activity.ParentSpanId)),
        };
        ActivitySource.AddActivityListener(listener);
        using var metrics = new DurationBag();
        using var parent = WorkerTelemetry.Source.StartActivity("vies-queue iteration");
        Assert.NotNull(parent);

        var client = new ViesVatChecker(
            new FixedOptions(new ViesClientOptions { Endpoint = "http://127.0.0.1:1", TimeoutSeconds = 1 }),
            NullLogger<ViesVatChecker>.Instance);

        await Assert.ThrowsAnyAsync<Exception>(() => client.CheckAsync("DE", "123456789", CancellationToken.None));

        var soap = Assert.Single(stopped, activity => activity.Name == ViesTelemetry.CheckVatSpanName);
        Assert.Equal(ActivityStatusCode.Error, soap.Status);
        Assert.Equal(parent.SpanId, soap.Parent);
        Assert.Contains(
            metrics.Samples,
            sample => sample.Name == ViesTelemetry.CallDurationName && sample.Outcome == "failure" && sample.Value >= 0);
    }

    private sealed class FixedOptions(ViesClientOptions value) : IOptionsMonitor<ViesClientOptions>
    {
        public ViesClientOptions CurrentValue { get; } = value;
        public ViesClientOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ViesClientOptions, string?> listener) => null;
    }

    private sealed class DurationBag : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<Sample> Samples { get; } = [];

        public DurationBag()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ViesTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Samples.Add(new Sample(instrument.Name, value, Tag(tags, "outcome"))));
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();

        private static string? Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == key)
                {
                    return tag.Value?.ToString();
                }
            }

            return null;
        }
    }

    private sealed record Sample(string Name, double Value, string? Outcome);
}
