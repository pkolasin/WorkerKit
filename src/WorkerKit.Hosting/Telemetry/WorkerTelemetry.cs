using System.Diagnostics;
using System.Diagnostics.Metrics;

using WorkerKit.Abstractions.Queues;
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// <see cref="ActivitySource"/> and <see cref="Meter"/> for worker iterations and health.
/// The meter comes from <see cref="IMeterFactory"/> so the OpenTelemetry meter provider listens to it.
/// </summary>
public sealed class WorkerTelemetry
{
    // service.name comes from WorkerAppIdentity, not from these constants.
    public const string SourceName = "WorkerKit";
    public const string MeterName = "WorkerKit";
    public const string QueuePendingName = "worker.queue.pending";

    public static ActivitySource Source { get; } = new(SourceName, TelemetryIdentity.ReadServiceVersion());

    private readonly IWorkerHeartbeat _heartbeat;
    private readonly IReadOnlyList<IQueuePendingSource> _pending;
    private readonly Counter<long> _iterations;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _items;
    private HealthReportHolder _health = new(null);

    public WorkerTelemetry(
        IMeterFactory meterFactory,
        IWorkerHeartbeat heartbeat,
        IEnumerable<IQueuePendingSource>? pendingSources = null)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        ArgumentNullException.ThrowIfNull(heartbeat);
        _heartbeat = heartbeat;
        _pending = pendingSources?.ToArray() ?? [];
        var version = TelemetryIdentity.ReadServiceVersion();
        var meter = meterFactory.Create(new MeterOptions(MeterName) { Version = version });
        _iterations = meter.CreateCounter<long>(
            "worker.iterations",
            description: "Worker iterations by outcome.");
        _duration = meter.CreateHistogram<double>(
            "worker.iteration.duration",
            unit: "s",
            description: "Worker iteration duration.");
        _items = meter.CreateCounter<long>(
            "worker.items.processed",
            description: "Items processed. Queue workers set outcome to done, retry, or failed.");
        meter.CreateObservableGauge(
            QueuePendingName,
            ObservePending,
            description: "Items waiting to be claimed.");
        meter.CreateObservableGauge(
            "worker.last_success.time",
            ObserveLastSuccess,
            unit: "s",
            description: "Unix time of the last successful iteration.");
        meter.CreateObservableGauge(
            "worker.health.status",
            ObserveHealth,
            description: "Latest health status: 0 unhealthy, 1 degraded, 2 healthy.");
    }

    public void RecordIteration(string workerName, string outcome, TimeSpan duration, int itemCount)
    {
        var tags = new TagList
        {
            { "worker", workerName },
            { "outcome", outcome },
        };
        _iterations.Add(1, tags);
        _duration.Record(duration.TotalSeconds, tags);
        if (itemCount > 0)
        {
            _items.Add(itemCount, new TagList { { "worker", workerName } });
        }
    }

    /// <summary>
    /// Counts one queue item. <paramref name="outcome"/> is <c>done</c>, <c>retry</c>, or <c>failed</c>.
    /// </summary>
    public void RecordItem(string workerName, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        _items.Add(1, new TagList
        {
            { "worker", workerName },
            { "outcome", outcome },
        });
    }

    public void RecordHealth(Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _health = new HealthReportHolder(report);
    }

    private IEnumerable<Measurement<long>> ObservePending()
    {
        foreach (var source in _pending)
        {
            if (source.PendingCount is { } count)
            {
                yield return new Measurement<long>(count, new TagList { { "worker", source.Name } });
            }
        }
    }

    private IEnumerable<Measurement<long>> ObserveLastSuccess()
    {
        foreach (var snapshot in _heartbeat.Snapshot())
        {
            if (snapshot.LastSuccess is { } success)
            {
                yield return new Measurement<long>(
                    success.ToUnixTimeSeconds(),
                    new TagList { { "worker", snapshot.Name } });
            }
        }
    }

    private IEnumerable<Measurement<int>> ObserveHealth()
    {
        var report = _health.Report;
        if (report is null)
        {
            yield break;
        }

        yield return new Measurement<int>((int)report.Status, new TagList { { "check", "report" } });
        foreach (var (name, entry) in report.Entries)
        {
            yield return new Measurement<int>((int)entry.Status, new TagList { { "check", name } });
        }
    }

    private sealed class HealthReportHolder(Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport? report)
    {
        public Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport? Report { get; } = report;
    }
}
