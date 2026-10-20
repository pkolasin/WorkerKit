using System.Diagnostics;
using System.Diagnostics.Metrics;

using WorkerKit.Abstractions.Queues;
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// <see cref="ActivitySource"/> and <see cref="Meter"/> for worker iterations and health.
/// The meter comes from <see cref="IMeterFactory"/> so the OpenTelemetry meter provider listens to it.
/// </summary>
/// <remarks>
/// The meter publishes <c>worker.iterations</c>, <c>worker.iteration.duration</c> in seconds, <c>worker.items.processed</c>,
/// <see cref="QueuePendingName"/>, <c>worker.last_success.time</c> as Unix seconds, and <c>worker.health.status</c>.
/// The health gauge uses the <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus"/> numbers: 0 unhealthy, 1 degraded, 2 healthy.
/// A queue with a null <see cref="IQueuePendingSource.PendingCount"/> is omitted. A worker with no successful iteration is omitted from the last-success gauge.
/// </remarks>
public sealed class WorkerTelemetry
{
    // service.name comes from WorkerAppIdentity, not from these constants.
    /// <summary>Activity source name <c>WorkerKit</c>. It is not the <c>service.name</c> resource attribute.</summary>
    public const string SourceName = "WorkerKit";

    /// <summary>Meter name <c>WorkerKit</c>.</summary>
    public const string MeterName = "WorkerKit";

    /// <summary>Gauge name <c>worker.queue.pending</c>.</summary>
    public const string QueuePendingName = "worker.queue.pending";

    /// <summary><see cref="ActivitySource"/> named <see cref="SourceName"/>, versioned with <see cref="TelemetryIdentity.ReadServiceVersion"/>.</summary>
    public static ActivitySource Source { get; } = new(SourceName, TelemetryIdentity.ReadServiceVersion());

    private readonly IWorkerHeartbeat _heartbeat;
    private readonly IReadOnlyList<IQueuePendingSource> _pending;
    private readonly Counter<long> _iterations;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _items;
    private HealthReportHolder _health = new(null);

    /// <summary>
    /// Creates the iteration counters and the pending, last-success, and health gauges on a meter from <paramref name="meterFactory"/>.
    /// </summary>
    /// <param name="meterFactory">Factory whose meter the OpenTelemetry meter provider listens to.</param>
    /// <param name="heartbeat">Supplies last-success timestamps for <c>worker.last_success.time</c>.</param>
    /// <param name="pendingSources">Queues reported by <see cref="QueuePendingName"/>. Null is treated as an empty list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="meterFactory"/> or <paramref name="heartbeat"/> is null.</exception>
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

    /// <summary>
    /// Adds one <c>worker.iterations</c> sample tagged with <paramref name="workerName"/> and <paramref name="outcome"/>,
    /// and records <paramref name="duration"/> in seconds on <c>worker.iteration.duration</c>.
    /// When <paramref name="itemCount"/> is greater than zero, adds that many to <c>worker.items.processed</c> with only the worker tag.
    /// </summary>
    /// <param name="workerName">Value of the <c>worker</c> tag.</param>
    /// <param name="outcome">Value of the <c>outcome</c> tag.</param>
    /// <param name="duration">Recorded in seconds.</param>
    /// <param name="itemCount">Added to the items counter only when it is greater than zero. That sample has no outcome tag.</param>
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
    /// <param name="workerName">Value of the <c>worker</c> tag.</param>
    /// <param name="outcome">Value of the <c>outcome</c> tag.</param>
    /// <exception cref="ArgumentNullException"><paramref name="workerName"/> or <paramref name="outcome"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workerName"/> or <paramref name="outcome"/> is empty or white space.</exception>
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

    /// <summary>
    /// Stores <paramref name="report"/> for the <c>worker.health.status</c> gauge.
    /// The next collection reports the overall status with check <c>report</c> and each entry under its check name.
    /// </summary>
    /// <param name="report">Report to publish. Its status is cast to the gauge value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
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
