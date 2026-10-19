using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WorkerKit.Hosting.Health;

public sealed class LoggingHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly ILogger<LoggingHealthCheckPublisher> _logger;

    public LoggingHealthCheckPublisher(ILogger<LoggingHealthCheckPublisher> logger)
    {
        _logger = logger;
    }

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
