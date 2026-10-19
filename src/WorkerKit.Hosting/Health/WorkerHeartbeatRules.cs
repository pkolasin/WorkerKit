using Microsoft.Extensions.Diagnostics.HealthChecks;

using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Health;

/// <summary>
/// Maps one heartbeat onto Healthy, Degraded, or Unhealthy.
/// Before the first success, the worker stays Healthy for the grace period.
/// After that, a success older than the grace period is Unhealthy (the loop looks stuck).
/// Failures that are still inside the grace period are Degraded.
/// </summary>
public static class WorkerHeartbeatRules
{
    public static HealthStatus Evaluate(WorkerHeartbeatSnapshot worker, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(worker);
        if (worker.LastSuccess is null && now - worker.Registered < worker.Grace)
        {
            return HealthStatus.Healthy;
        }

        if (now - (worker.LastSuccess ?? worker.Registered) > worker.Grace)
        {
            return HealthStatus.Unhealthy;
        }

        if (worker.ConsecutiveFailures > 0)
        {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }

    /// <summary>
    /// <see cref="HealthStatus"/> is ordered Unhealthy, Degraded, Healthy. Lower is worse.
    /// </summary>
    public static HealthStatus Worse(HealthStatus current, HealthStatus candidate)
    {
        return Rank(candidate) < Rank(current) ? candidate : current;
    }

    public static int Rank(HealthStatus status) => status switch
    {
        HealthStatus.Unhealthy => 0,
        HealthStatus.Degraded => 1,
        HealthStatus.Healthy => 2,
        _ => 1,
    };
}
