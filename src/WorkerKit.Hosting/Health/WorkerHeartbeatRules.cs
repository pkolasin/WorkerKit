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
    /// <summary>
    /// Returns Healthy when <paramref name="worker"/> has never succeeded and the time since registration is less than its grace.
    /// Returns Unhealthy when the time since the last success, or since registration when there is no success, is greater than the grace.
    /// Returns Degraded when the age is not greater than the grace and the consecutive failure count is greater than zero.
    /// Otherwise returns Healthy.
    /// </summary>
    /// <param name="worker">Snapshot to score. <see cref="WorkerHeartbeatSnapshot.Grace"/> is the allowed age.</param>
    /// <param name="now">Instant compared with registration and last success.</param>
    /// <returns><see cref="HealthStatus.Healthy"/>, <see cref="HealthStatus.Degraded"/>, or <see cref="HealthStatus.Unhealthy"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="worker"/> is null.</exception>
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
    /// <param name="current">Status already chosen.</param>
    /// <param name="candidate">Status compared with <paramref name="current"/>.</param>
    /// <returns><paramref name="candidate"/> when <see cref="Rank"/> is lower for it; otherwise <paramref name="current"/>.</returns>
    public static HealthStatus Worse(HealthStatus current, HealthStatus candidate)
    {
        return Rank(candidate) < Rank(current) ? candidate : current;
    }

    /// <summary>
    /// Maps <paramref name="status"/> to a rank. Unhealthy is 0, Degraded is 1, and Healthy is 2.
    /// Any other value is 1.
    /// </summary>
    /// <param name="status">Status to rank.</param>
    /// <returns>0, 1, or 2. A lower number is worse.</returns>
    public static int Rank(HealthStatus status) => status switch
    {
        HealthStatus.Unhealthy => 0,
        HealthStatus.Degraded => 1,
        HealthStatus.Healthy => 2,
        _ => 1,
    };
}
