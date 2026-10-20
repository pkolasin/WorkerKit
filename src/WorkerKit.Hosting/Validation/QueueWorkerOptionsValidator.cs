
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

/// <summary>
/// Checks lease, retry, and idle limits for <see cref="QueueWorkerOptions"/> against <paramref name="health"/>.
/// </summary>
/// <param name="health">Health options used to bound <see cref="QueueWorkerOptions.MaxIdleDelay"/>.</param>
public sealed class QueueWorkerOptionsValidator(IOptionsMonitor<HealthOptions> health) : IValidateOptions<QueueWorkerOptions>
{
    /// <summary>
    /// Requires a positive lease of at most one day, a retry delay from zero through one hour,
    /// a positive minimum idle delay, and a maximum idle delay at least that large.
    /// When health options can be read, the maximum idle delay must not exceed the grace period for the minimum idle delay.
    /// </summary>
    /// <param name="name">Named-options name. An empty name uses <see cref="QueueWorkerOptions.SectionName"/> in failure text.</param>
    /// <param name="options">Options to check.</param>
    /// <returns>Success, or the collected failures.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <remarks>
    /// The grace check calls <see cref="HealthOptions.HeartbeatGraceFor"/>. It is skipped when reading
    /// <see cref="IOptionsMonitor{TOptions}.CurrentValue"/> throws <see cref="OptionsValidationException"/>.
    /// </remarks>
    public ValidateOptionsResult Validate(string? name, QueueWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var prefix = string.IsNullOrEmpty(name) ? QueueWorkerOptions.SectionName : $"{QueueWorkerOptions.SectionName}:{name}";
        var errors = new List<string>();
        if (options.LeaseDuration <= TimeSpan.Zero || options.LeaseDuration > TimeSpan.FromDays(1))
        {
            errors.Add($"{prefix}:LeaseDuration must be positive and at most 1 day.");
        }

        if (options.RetryDelay < TimeSpan.Zero || options.RetryDelay > TimeSpan.FromHours(1))
        {
            errors.Add($"{prefix}:RetryDelay must be zero or positive and at most 1 hour.");
        }

        if (options.MinIdleDelay <= TimeSpan.Zero)
        {
            errors.Add($"{prefix}:MinIdleDelay must be positive.");
        }
        else if (options.MaxIdleDelay < options.MinIdleDelay)
        {
            errors.Add($"{prefix}:MaxIdleDelay must be greater than or equal to MinIdleDelay.");
        }
        else if (TryGetHealth(out var healthOptions))
        {
            var grace = healthOptions.HeartbeatGraceFor(options.MinIdleDelay);
            if (options.MaxIdleDelay > grace)
            {
                errors.Add(
                    $"{prefix}:MaxIdleDelay ({options.MaxIdleDelay}) exceeds the heartbeat grace ({grace}) for MinIdleDelay. An idle queue would look stuck.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private bool TryGetHealth(out HealthOptions options)
    {
        try
        {
            options = health.CurrentValue;
            return true;
        }
        catch (OptionsValidationException)
        {
            // Health options report their own startup error.
            options = null!;
            return false;
        }
    }
}
