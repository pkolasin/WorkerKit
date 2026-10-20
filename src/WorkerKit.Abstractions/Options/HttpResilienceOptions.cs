using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Per-client resilience settings mapped onto the standard HTTP resilience handler
/// (retry with exponential backoff, per-attempt timeout, total timeout, circuit breaker).
/// </summary>
public sealed class HttpResilienceOptions
{
    /// <summary>Maximum number of retries.</summary>
    /// <remarks>The initializer is <c>3</c>. <see cref="RangeAttribute"/> allows 1 through 10.</remarks>
    [Range(1, 10, ErrorMessage = "MaxRetryAttempts must be between 1 and 10.")]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Retry delay, in seconds.</summary>
    /// <remarks>The initializer is <c>1</c>. <see cref="RangeAttribute"/> allows 0 through 60.</remarks>
    [Range(0, 60, ErrorMessage = "RetryDelaySeconds must be between 0 and 60.")]
    public int RetryDelaySeconds { get; set; } = 1;

    /// <summary>Per-attempt timeout, in seconds.</summary>
    /// <remarks>The initializer is <c>10</c>. <see cref="RangeAttribute"/> allows 1 through 300.</remarks>
    [Range(1, 300, ErrorMessage = "AttemptTimeoutSeconds must be between 1 and 300.")]
    public int AttemptTimeoutSeconds { get; set; } = 10;

    /// <summary>Total request timeout, in seconds.</summary>
    /// <remarks>The initializer is <c>60</c>. <see cref="RangeAttribute"/> allows 1 through 600.</remarks>
    [Range(1, 600, ErrorMessage = "TotalRequestTimeoutSeconds must be between 1 and 600.")]
    public int TotalRequestTimeoutSeconds { get; set; } = 60;

    /// <summary>Circuit-breaker sampling window, in seconds.</summary>
    /// <remarks>The initializer is <c>30</c>. <see cref="RangeAttribute"/> allows 2 through 600.</remarks>
    [Range(2, 600, ErrorMessage = "CircuitBreakerSamplingSeconds must be between 2 and 600.")]
    public int CircuitBreakerSamplingSeconds { get; set; } = 30;

    /// <summary>Failure ratio that opens the circuit.</summary>
    /// <remarks>The initializer is <c>0.5</c>. <see cref="RangeAttribute"/> allows 0.01 through 1.</remarks>
    [Range(0.01, 1, ErrorMessage = "CircuitBreakerFailureRatio must be between 0.01 and 1.")]
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>Minimum throughput for the circuit breaker.</summary>
    /// <remarks>The initializer is <c>5</c>. <see cref="RangeAttribute"/> allows 2 through 10000.</remarks>
    [Range(2, 10_000, ErrorMessage = "CircuitBreakerMinimumThroughput must be between 2 and 10000.")]
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>How long the circuit stays open, in seconds.</summary>
    /// <remarks>The initializer is <c>30</c>. <see cref="RangeAttribute"/> allows 1 through 600.</remarks>
    [Range(1, 600, ErrorMessage = "CircuitBreakerBreakDurationSeconds must be between 1 and 600.")]
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;
}
