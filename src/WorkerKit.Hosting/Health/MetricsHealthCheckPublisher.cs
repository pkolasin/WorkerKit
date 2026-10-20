using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkerKit.Hosting.Telemetry;

namespace WorkerKit.Hosting.Health;

/// <summary>
/// Stores the latest health report on <see cref="WorkerTelemetry"/> so <c>worker.health.status</c> can export it.
/// </summary>
public sealed class MetricsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly WorkerTelemetry _telemetry;

    /// <summary>Stores the telemetry object that keeps the latest report.</summary>
    /// <param name="telemetry">Receives <see cref="WorkerTelemetry.RecordHealth"/>.</param>
    public MetricsHealthCheckPublisher(WorkerTelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    /// <summary>Forwards <paramref name="report"/> to <see cref="WorkerTelemetry.RecordHealth"/>.</summary>
    /// <param name="report">Latest health report.</param>
    /// <param name="cancellationToken">Unused. The report is stored synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        _telemetry.RecordHealth(report);
        return Task.CompletedTask;
    }
}
