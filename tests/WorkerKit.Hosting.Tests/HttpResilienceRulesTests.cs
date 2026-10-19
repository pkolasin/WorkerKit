
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Tests;

public sealed class HttpResilienceRulesTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var failures = HttpResilienceRules.Evaluate(new HttpResilienceOptions(), "OpenMeteo:Resilience");

        Assert.Empty(failures);
    }

    [Fact]
    public void Sampling_window_must_cover_two_attempts()
    {
        var options = new HttpResilienceOptions
        {
            AttemptTimeoutSeconds = 20,
            TotalRequestTimeoutSeconds = 60,
            CircuitBreakerSamplingSeconds = 30,
        };

        var failures = HttpResilienceRules.Evaluate(options, "OpenMeteo:Resilience");

        Assert.Contains(failures, failure => failure.Contains("CircuitBreakerSamplingSeconds", StringComparison.Ordinal));
    }
}
