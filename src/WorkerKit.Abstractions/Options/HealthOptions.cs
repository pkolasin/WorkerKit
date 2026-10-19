using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

public sealed class HealthOptions
{
    public const string SectionName = "Health";

    [Range(1, 86_400, ErrorMessage = "Health:PublisherPeriodSeconds must be between 1 and 86400.")]
    public int PublisherPeriodSeconds { get; set; } = 60;

    /// <summary>
    /// Absolute HTTP prefix ending with '/'. Empty disables the endpoint.
    /// The host must be loopback. Example: http://127.0.0.1:5088/health/
    /// </summary>
    public string ListenUrl { get; set; } = "";

    /// <summary>
    /// A worker is stale when the time since its last success (or since registration, if it has never
    /// succeeded) is greater than the grace period. Grace is the larger of
    /// <c>interval * HeartbeatStaleMultiplier</c> and <c>interval + HeartbeatGracePadding</c>.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Health:HeartbeatStaleMultiplier must be between 1 and 100.")]
    public int HeartbeatStaleMultiplier { get; set; } = 3;

    /// <summary>
    /// Added to the worker interval when computing the minimum grace period. Default is 5 minutes,
    /// so a short interval cannot go unhealthy after only a few seconds.
    /// </summary>
    public TimeSpan HeartbeatGracePadding { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan HeartbeatGraceFor(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Worker interval must be positive.");
        }

        if (HeartbeatStaleMultiplier < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HeartbeatStaleMultiplier),
                "Health:HeartbeatStaleMultiplier must be at least 1.");
        }

        if (HeartbeatGracePadding < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HeartbeatGracePadding),
                "Health:HeartbeatGracePadding must be zero or positive.");
        }

        var scaled = interval * HeartbeatStaleMultiplier;
        var padded = interval + HeartbeatGracePadding;
        return scaled >= padded ? scaled : padded;
    }
}
