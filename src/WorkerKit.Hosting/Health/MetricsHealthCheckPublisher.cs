using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkerKit.Hosting.Telemetry;

namespace WorkerKit.Hosting.Health;

/// <summary>
/// Stores the latest health report on <see cref="WorkerTelemetry"/> so <c>worker.health.status</c> can export it.
/// </summary>
public sealed class MetricsHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly WorkerTelemetry _telemetry;

    public MetricsHealthCheckPublisher(WorkerTelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        _telemetry.RecordHealth(report);
        return Task.CompletedTask;
    }
}
