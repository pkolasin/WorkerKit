using Microsoft.Extensions.Http.Resilience;
using Polly;

using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Http;

internal static class ResilienceConfigurator
{
    public static void Apply(HttpStandardResilienceOptions target, HttpResilienceOptions source)
    {
        target.Retry.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Retry.Delay = TimeSpan.FromSeconds(source.RetryDelaySeconds);
        target.Retry.BackoffType = DelayBackoffType.Exponential;
        target.Retry.UseJitter = true;
        target.AttemptTimeout.Timeout = TimeSpan.FromSeconds(source.AttemptTimeoutSeconds);
        target.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(source.TotalRequestTimeoutSeconds);
        target.CircuitBreaker.FailureRatio = source.CircuitBreakerFailureRatio;
        target.CircuitBreaker.MinimumThroughput = source.CircuitBreakerMinimumThroughput;
        target.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(source.CircuitBreakerSamplingSeconds);
        target.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(source.CircuitBreakerBreakDurationSeconds);
    }
}
