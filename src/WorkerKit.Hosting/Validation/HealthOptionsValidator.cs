
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

public sealed class HealthOptionsValidator : IValidateOptions<HealthOptions>
{
    public ValidateOptionsResult Validate(string? name, HealthOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ListenUrl))
        {
            return ValidateOptionsResult.Success;
        }

        var url = options.ListenUrl.Trim();
        if (!url.EndsWith('/'))
        {
            return ValidateOptionsResult.Fail("Health:ListenUrl must end with '/' (HttpListener prefix).");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
        {
            return ValidateOptionsResult.Fail("Health:ListenUrl must be an absolute http:// prefix. HTTPS is not enabled on the sample listener.");
        }

        if (!uri.IsLoopback)
        {
            return ValidateOptionsResult.Fail("Health:ListenUrl must listen on 127.0.0.1, localhost, or ::1.");
        }

        if (options.HeartbeatGracePadding < TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("Health:HeartbeatGracePadding must be zero or positive.");
        }

        return ValidateOptionsResult.Success;
    }
}
