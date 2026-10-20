using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Health;

/// <summary>
/// Scores <see cref="IWorkerHeartbeat.Snapshot"/> with <see cref="WorkerHeartbeatRules.Evaluate"/>.
/// The result status is the worst worker. An empty snapshot is Healthy.
/// </summary>
public sealed class WorkerHeartbeatHealthCheck : IHealthCheck
{
    private readonly IWorkerHeartbeat _heartbeat;
    private readonly TimeProvider _time;

    /// <summary>Stores the heartbeat source and the clock used as the current time.</summary>
    /// <param name="heartbeat">Registered workers to score.</param>
    /// <param name="time">Supplies <see cref="TimeProvider.GetUtcNow"/> for the grace comparison.</param>
    public WorkerHeartbeatHealthCheck(IWorkerHeartbeat heartbeat, TimeProvider time)
    {
        _heartbeat = heartbeat;
        _time = time;
    }

    /// <summary>
    /// Scores every snapshot at <see cref="TimeProvider.GetUtcNow"/>.
    /// Each worker is stored in the result data with status, last success, consecutive failures, last error, and grace.
    /// </summary>
    /// <param name="context">Health check context. This check does not read it.</param>
    /// <param name="cancellationToken">Unused. The snapshot is read synchronously.</param>
    /// <returns>A result described as <c>worker heartbeats</c> whose status is the worst worker.</returns>
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
