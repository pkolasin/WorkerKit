namespace WorkerKit.Samples.Vies;

public sealed class ViesClientOptionsValidator : IValidateOptions<ViesClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ViesClientOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Success;
        }

        if (!Uri.TryCreate(options.Endpoint.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail("Vies:Endpoint must be an absolute http or https URI.");
        }

        return ValidateOptionsResult.Success;
    }
}
