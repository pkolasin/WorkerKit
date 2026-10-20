using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WorkerKit.Hosting.Health;

/// <summary>
/// Writes the health report to the log. A Healthy report is Information. Every other status is Warning.
/// </summary>
public sealed class LoggingHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<LoggingHealthCheckPublisher> _logger;

    /// <summary>Stores the logger used by <see cref="PublishAsync"/>.</summary>
    /// <param name="logger">Destination for the status line and one line per check.</param>
    public LoggingHealthCheckPublisher(ILogger<LoggingHealthCheckPublisher> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs the report status, total duration in milliseconds, and each check name, status, and description.
    /// </summary>
    /// <param name="report">Report published by the health check service.</param>
    /// <param name="cancellationToken">Unused. Logging completes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var level = report.Status == HealthStatus.Healthy ? LogLevel.Information : LogLevel.Warning;
        _logger.Log(
            level,
            "Health status {Status} after {DurationMs} ms",
            report.Status,
            report.TotalDuration.TotalMilliseconds);

        foreach (var entry in report.Entries)
        {
            _logger.Log(
                level,
                "Health check {Check} is {Status}: {Description}",
                entry.Key,
                entry.Value.Status,
                entry.Value.Description);
        }

        return Task.CompletedTask;
    }
}
