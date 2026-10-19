using Microsoft.Extensions.Http.Resilience;
using WorkerKit.Hosting.DependencyInjection;
using WorkerKit.Hosting.Validation;

using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Http;

public static class HttpClientRegistration
{
    /// <summary>
    /// Registers a typed <see cref="IHttpClientFactory"/> client and the standard resilience handler.
    /// Options, including retry, timeout, and circuit breaker, come from <paramref name="section"/>.
    /// Typed clients are transient: resolve them from a scope, not from a singleton.
    /// <see cref="HttpClient.Timeout"/> is infinite because the resilience handler owns the timeouts.
    /// </summary>
    public static IHttpClientBuilder AddResilientJsonClient<TClient, TImplementation, TOptions>(
        this IServiceCollection services,
        IConfiguration section)
        where TClient : class
        where TImplementation : class, TClient
        where TOptions : HttpClientEndpointOptions, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.AddValidatedOptions<TOptions>(section);
        services.AddSingleton<IValidateOptions<TOptions>, HttpClientEndpointOptionsValidator<TOptions>>();

        var builder = services.AddHttpClient<TClient, TImplementation>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TOptions>>().Value;
            var address = options.BaseAddress.Trim();
            if (!address.EndsWith('/'))
            {
                address += "/";
            }

            client.BaseAddress = new Uri(address, UriKind.Absolute);
            client.Timeout = Timeout.InfiniteTimeSpan;
            if (!string.IsNullOrWhiteSpace(options.UserAgent))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            }
        });

        builder.AddStandardResilienceHandler().Configure((resilience, serviceProvider) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TOptions>>().Value;
            ResilienceConfigurator.Apply(resilience, options.Resilience);
        });

        return builder;
    }
}
