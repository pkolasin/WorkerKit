
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

/// <summary>
/// Checks <see cref="HttpClientEndpointOptions.BaseAddress"/> and <see cref="HttpClientEndpointOptions.Resilience"/>
/// for <typeparamref name="TOptions"/>.
/// </summary>
/// <typeparam name="TOptions">Endpoint options type.</typeparam>
public sealed class HttpClientEndpointOptionsValidator<TOptions> : IValidateOptions<TOptions>
    where TOptions : HttpClientEndpointOptions
{
    /// <summary>
    /// Requires an absolute http or https base address when one is set, and appends failures from <see cref="HttpResilienceRules.Evaluate"/>.
    /// </summary>
    /// <param name="name">Named-options name. This validator does not use it.</param>
    /// <param name="options">Options to check.</param>
    /// <returns>Success, or the collected failures.</returns>
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        var failures = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.BaseAddress) && !IsHttpUri(options.BaseAddress))
        {
            failures.Add($"{typeof(TOptions).Name}: BaseAddress must be an absolute http or https URI.");
        }

        failures.AddRange(HttpResilienceRules.Evaluate(options.Resilience, $"{typeof(TOptions).Name}:Resilience"));
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsHttpUri(string value)
    {
        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
