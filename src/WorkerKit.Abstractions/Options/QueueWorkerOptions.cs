using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Settings for one <c>QueueWorker&lt;T&gt;</c>. Bind a named instance per worker: <c>Queue:{worker name}</c>.
/// </summary>
public sealed class QueueWorkerOptions
{
    public const string SectionName = "Queue";

    /// <summary>
    /// When false the hosted worker returns without registering a heartbeat.
    /// The example queue worker defaults to false.
    /// </summary>
    public bool Enabled { get; set; }

    [Range(1, 10_000, ErrorMessage = "Queue BatchSize must be between 1 and 10000.")]
    public int BatchSize { get; set; } = 10;

    [Range(1, 100, ErrorMessage = "Queue MaxConcurrency must be between 1 and 100.")]
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>How long a claim owns an item. After this, another worker may claim it.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 100, ErrorMessage = "Queue MaxAttempts must be between 1 and 100.")]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base delay before a released item can be claimed again. The worker multiplies it by the attempt.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Delay after an iteration that received work, and the start of the empty-queue backoff.</summary>
    public TimeSpan MinIdleDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound of the empty-queue backoff. Must stay inside the heartbeat grace period.</summary>
    public TimeSpan MaxIdleDelay { get; set; } = TimeSpan.FromSeconds(30);
}
