namespace WorkerKit.Abstractions.Options;

/// <summary>Checks a <see cref="HttpResilienceOptions"/> value and returns the rules it breaks.</summary>
public static class HttpResilienceRules
{
    /// <summary>Checks <paramref name="options"/> and returns a message for each rule that fails.</summary>
    /// <param name="options">Settings to check.</param>
    /// <param name="prefix">Text placed before a range message. A null or empty prefix is omitted from those messages.</param>
    /// <returns>The failure messages, in check order. The list is empty when every rule passes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Inclusive bounds: <see cref="HttpResilienceOptions.MaxRetryAttempts"/> 1 through 10,
    /// <see cref="HttpResilienceOptions.RetryDelaySeconds"/> 0 through 60,
    /// <see cref="HttpResilienceOptions.AttemptTimeoutSeconds"/> 1 through 300,
    /// <see cref="HttpResilienceOptions.TotalRequestTimeoutSeconds"/> 1 through 600,
    /// <see cref="HttpResilienceOptions.CircuitBreakerSamplingSeconds"/> 2 through 600,
    /// <see cref="HttpResilienceOptions.CircuitBreakerFailureRatio"/> 0.01 through 1,
    /// <see cref="HttpResilienceOptions.CircuitBreakerMinimumThroughput"/> 2 through 10000,
    /// and <see cref="HttpResilienceOptions.CircuitBreakerBreakDurationSeconds"/> 1 through 600.
    /// A null or empty <paramref name="prefix"/> leaves these messages unchanged. Any other prefix is written as
    /// <paramref name="prefix"/>, then <c>: </c>, then the message.
    /// </para>
    /// <para>
    /// <see cref="HttpResilienceOptions.TotalRequestTimeoutSeconds"/> must be greater than
    /// <see cref="HttpResilienceOptions.AttemptTimeoutSeconds"/>.
    /// <see cref="HttpResilienceOptions.CircuitBreakerSamplingSeconds"/> must be at least twice
    /// <see cref="HttpResilienceOptions.AttemptTimeoutSeconds"/>.
    /// Those two messages are always prefixed with <paramref name="prefix"/> and <c>: </c>.
    /// </para>
    /// </remarks>
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
