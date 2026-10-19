
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

public sealed class HttpClientEndpointOptionsValidator<TOptions> : IValidateOptions<TOptions>
    where TOptions : HttpClientEndpointOptions
{
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
