namespace WorkerKit.Abstractions.Options;

public static class HttpResilienceRules
{
    public static IReadOnlyList<string> Evaluate(HttpResilienceOptions options, string prefix)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        AddRange(failures, prefix, options.MaxRetryAttempts, 1, 10, "MaxRetryAttempts must be between 1 and 10.");
        AddRange(failures, prefix, options.RetryDelaySeconds, 0, 60, "RetryDelaySeconds must be between 0 and 60.");
        AddRange(failures, prefix, options.AttemptTimeoutSeconds, 1, 300, "AttemptTimeoutSeconds must be between 1 and 300.");
        AddRange(failures, prefix, options.TotalRequestTimeoutSeconds, 1, 600, "TotalRequestTimeoutSeconds must be between 1 and 600.");
        AddRange(failures, prefix, options.CircuitBreakerSamplingSeconds, 2, 600, "CircuitBreakerSamplingSeconds must be between 2 and 600.");
        AddRange(failures, prefix, options.CircuitBreakerFailureRatio, 0.01, 1, "CircuitBreakerFailureRatio must be between 0.01 and 1.");
        AddRange(failures, prefix, options.CircuitBreakerMinimumThroughput, 2, 10_000, "CircuitBreakerMinimumThroughput must be between 2 and 10000.");
        AddRange(failures, prefix, options.CircuitBreakerBreakDurationSeconds, 1, 600, "CircuitBreakerBreakDurationSeconds must be between 1 and 600.");

        if (options.TotalRequestTimeoutSeconds <= options.AttemptTimeoutSeconds)
        {
            failures.Add($"{prefix}: TotalRequestTimeoutSeconds must be greater than AttemptTimeoutSeconds.");
        }

        if (options.CircuitBreakerSamplingSeconds < options.AttemptTimeoutSeconds * 2)
        {
            failures.Add($"{prefix}: CircuitBreakerSamplingSeconds must be at least twice AttemptTimeoutSeconds.");
        }

        return failures;
    }

    private static void AddRange(ICollection<string> failures, string prefix, int value, int min, int max, string message)
    {
        if (value < min || value > max)
        {
            failures.Add(Prefix(prefix, message));
        }
    }

    private static void AddRange(ICollection<string> failures, string prefix, double value, double min, double max, string message)
    {
        if (value < min || value > max)
        {
            failures.Add(Prefix(prefix, message));
        }
    }

    private static string Prefix(string prefix, string message) =>
        string.IsNullOrEmpty(prefix) ? message : prefix + ": " + message;
}
