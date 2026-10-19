using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Health;

public sealed class WorkerHeartbeatHealthCheck : IHealthCheck
{
    private readonly IWorkerHeartbeat _heartbeat;
    private readonly TimeProvider _time;

    public WorkerHeartbeatHealthCheck(IWorkerHeartbeat heartbeat, TimeProvider time)
    {
        _heartbeat = heartbeat;
        _time = time;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var data = new Dictionary<string, object>(StringComparer.Ordinal);
        var worst = HealthStatus.Healthy;
        foreach (var worker in _heartbeat.Snapshot())
        {
            var status = WorkerHeartbeatRules.Evaluate(worker, now);
            data[worker.Name] = WorkerData(worker, status);
            worst = WorkerHeartbeatRules.Worse(worst, status);
        }

        return Task.FromResult(new HealthCheckResult(worst, "worker heartbeats", data: data));
    }

    private static Dictionary<string, object?> WorkerData(WorkerHeartbeatSnapshot worker, HealthStatus status)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = status.ToString(),
            ["lastSuccess"] = worker.LastSuccess,
            ["consecutiveFailures"] = worker.ConsecutiveFailures,
            ["lastError"] = worker.LastError,
            ["grace"] = worker.Grace.ToString("c", CultureInfo.InvariantCulture),
        };
    }
}
