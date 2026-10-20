
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

/// <summary>Checks <see cref="HealthOptions.ListenUrl"/> and <see cref="HealthOptions.HeartbeatGracePadding"/>.</summary>
public sealed class HealthOptionsValidator : IValidateOptions<HealthOptions>
{
    /// <summary>
    /// An empty listen URL succeeds. A URL must be an absolute <c>http://</c> prefix that ends with <c>/</c> and uses a loopback host.
    /// When a URL is set, <see cref="HealthOptions.HeartbeatGracePadding"/> must be zero or positive.
    /// </summary>
    /// <param name="name">Named-options name. This validator does not use it.</param>
    /// <param name="options">Options to check.</param>
    /// <returns>Success, or a failure that names the health setting that failed.</returns>
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
