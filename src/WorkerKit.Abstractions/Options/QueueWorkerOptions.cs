using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Settings for one <c>QueueWorker&lt;T&gt;</c>. Bind a named instance per worker: <c>Queue:{worker name}</c>.
/// </summary>
public sealed class QueueWorkerOptions
{
    /// <summary>Configuration section name. The value is <c>Queue</c>.</summary>
    public const string SectionName = "Queue";

    /// <summary>
    /// When false the hosted worker returns without registering a heartbeat.
    /// The example queue worker defaults to false.
    /// </summary>
    /// <remarks>The property has no initializer, so the value is <see langword="false"/>.</remarks>
    public bool Enabled { get; set; }

    /// <summary>Number of items claimed in one batch.</summary>
    /// <remarks>The initializer is <c>10</c>. <see cref="RangeAttribute"/> allows 1 through 10000.</remarks>
    [Range(1, 10_000, ErrorMessage = "Queue BatchSize must be between 1 and 10000.")]
    public int BatchSize { get; set; } = 10;

    /// <summary>Maximum number of items handled at the same time.</summary>
    /// <remarks>The initializer is <c>1</c>. <see cref="RangeAttribute"/> allows 1 through 100.</remarks>
    [Range(1, 100, ErrorMessage = "Queue MaxConcurrency must be between 1 and 100.")]
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>How long a claim owns an item. After this, another worker may claim it.</summary>
    /// <remarks>The initializer is <c>TimeSpan.FromMinutes(1)</c>.</remarks>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Maximum number of attempts for one item.</summary>
    /// <remarks>The initializer is <c>5</c>. <see cref="RangeAttribute"/> allows 1 through 100.</remarks>
    [Range(1, 100, ErrorMessage = "Queue MaxAttempts must be between 1 and 100.")]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base delay before a released item can be claimed again. The worker multiplies it by the attempt.</summary>
    /// <remarks>The initializer is <c>TimeSpan.FromSeconds(5)</c>.</remarks>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Delay after an iteration that received work, and the start of the empty-queue backoff.</summary>
    /// <remarks>The initializer is <c>TimeSpan.FromSeconds(1)</c>.</remarks>
    public TimeSpan MinIdleDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound of the empty-queue backoff. Must stay inside the heartbeat grace period.</summary>
    /// <remarks>The initializer is <c>TimeSpan.FromSeconds(30)</c>.</remarks>
    public TimeSpan MaxIdleDelay { get; set; } = TimeSpan.FromSeconds(30);
}
