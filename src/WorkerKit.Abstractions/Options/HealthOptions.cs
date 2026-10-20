using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Health publisher period, optional listen URL, and the inputs used to decide that a worker heartbeat is stale.
/// </summary>
public sealed class HealthOptions
{
    /// <summary>Configuration section name. The value is <c>Health</c>.</summary>
    public const string SectionName = "Health";

    /// <summary>Health publisher period, in seconds.</summary>
    /// <remarks>The initializer is <c>60</c>. <see cref="RangeAttribute"/> allows 1 through 86400.</remarks>
    [Range(1, 86_400, ErrorMessage = "Health:PublisherPeriodSeconds must be between 1 and 86400.")]
    public int PublisherPeriodSeconds { get; set; } = 60;

    /// <summary>
    /// Absolute HTTP prefix ending with '/'. Empty disables the endpoint.
    /// The host must be loopback. Example: http://127.0.0.1:5088/health/
    /// </summary>
    /// <remarks>The initializer is <c>""</c>, which disables the endpoint.</remarks>
    public string ListenUrl { get; set; } = "";

    /// <summary>
    /// A worker is stale when the time since its last success (or since registration, if it has never
    /// succeeded) is greater than the grace period. Grace is the larger of
    /// <c>interval * HeartbeatStaleMultiplier</c> and <c>interval + HeartbeatGracePadding</c>.
    /// </summary>
    /// <remarks>The initializer is <c>3</c>. <see cref="RangeAttribute"/> allows 1 through 100.</remarks>
    [Range(1, 100, ErrorMessage = "Health:HeartbeatStaleMultiplier must be between 1 and 100.")]
    public int HeartbeatStaleMultiplier { get; set; } = 3;

    /// <summary>
    /// Added to the worker interval when computing the minimum grace period. Default is 5 minutes,
    /// so a short interval cannot go unhealthy after only a few seconds.
    /// </summary>
    /// <remarks>The initializer is <c>TimeSpan.FromMinutes(5)</c>.</remarks>
    public TimeSpan HeartbeatGracePadding { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Returns the grace period for a worker that runs every <paramref name="interval"/>.
    /// The result is the larger of <paramref name="interval"/> multiplied by <see cref="HeartbeatStaleMultiplier"/>
    /// and <paramref name="interval"/> plus <see cref="HeartbeatGracePadding"/>.
    /// </summary>
    /// <param name="interval">Worker interval. It must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <returns>The larger of the scaled interval and the padded interval.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is less than or equal to <see cref="TimeSpan.Zero"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="HeartbeatStaleMultiplier"/> is less than 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="HeartbeatGracePadding"/> is less than <see cref="TimeSpan.Zero"/>.</exception>
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
