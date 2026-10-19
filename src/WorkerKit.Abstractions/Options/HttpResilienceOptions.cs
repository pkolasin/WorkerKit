using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Per-client resilience settings mapped onto the standard HTTP resilience handler
/// (retry with exponential backoff, per-attempt timeout, total timeout, circuit breaker).
/// </summary>
public sealed class HttpResilienceOptions
{
    [Range(1, 10, ErrorMessage = "MaxRetryAttempts must be between 1 and 10.")]
    public int MaxRetryAttempts { get; set; } = 3;

    [Range(0, 60, ErrorMessage = "RetryDelaySeconds must be between 0 and 60.")]
    public int RetryDelaySeconds { get; set; } = 1;

    [Range(1, 300, ErrorMessage = "AttemptTimeoutSeconds must be between 1 and 300.")]
    public int AttemptTimeoutSeconds { get; set; } = 10;

    [Range(1, 600, ErrorMessage = "TotalRequestTimeoutSeconds must be between 1 and 600.")]
    public int TotalRequestTimeoutSeconds { get; set; } = 60;

    [Range(2, 600, ErrorMessage = "CircuitBreakerSamplingSeconds must be between 2 and 600.")]
    public int CircuitBreakerSamplingSeconds { get; set; } = 30;

    [Range(0.01, 1, ErrorMessage = "CircuitBreakerFailureRatio must be between 0.01 and 1.")]
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    [Range(2, 10_000, ErrorMessage = "CircuitBreakerMinimumThroughput must be between 2 and 10000.")]
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    [Range(1, 600, ErrorMessage = "CircuitBreakerBreakDurationSeconds must be between 1 and 600.")]
    public int CircuitBreakerBreakDurationSeconds { get; set; } = 30;
}
