using Microsoft.Extensions.Http.Resilience;
using WorkerKit.Hosting.DependencyInjection;
using WorkerKit.Hosting.Validation;

using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Http;

/// <summary>Registers typed HTTP clients that use the standard resilience handler.</summary>
public static class HttpClientRegistration
{
    /// <summary>
    /// Registers a typed <see cref="IHttpClientFactory"/> client and the standard resilience handler.
    /// Options, including retry, timeout, and circuit breaker, come from <paramref name="section"/>.
    /// Typed clients are transient: resolve them from a scope, not from a singleton.
    /// <see cref="HttpClient.Timeout"/> is infinite because the resilience handler owns the timeouts.
    /// </summary>
    /// <typeparam name="TClient">Client type resolved by callers.</typeparam>
    /// <typeparam name="TImplementation">Implementation registered for <typeparamref name="TClient"/>.</typeparam>
    /// <typeparam name="TOptions">Endpoint options bound from <paramref name="section"/>.</typeparam>
    /// <param name="services">Service collection to update.</param>
    /// <param name="section">Configuration section bound to <typeparamref name="TOptions"/>.</param>
    /// <returns>The HTTP client builder for <typeparamref name="TClient"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="section"/> is null.</exception>
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

    /// <summary>
    /// Registers a typed client for a large download.
    /// The client does not use the standard resilience handler, because that handler buffers the body so a retry can send it again.
    /// <see cref="HttpClient.Timeout"/> is infinite. The caller cancels with the request token.
    /// Connect timeout is <see cref="HttpResilienceOptions.AttemptTimeoutSeconds"/> from <typeparamref name="TOptions"/>.
    /// Call <see cref="SendStreamingAsync"/> or <see cref="HttpClient.GetStreamAsync(string, CancellationToken)"/> so the body is not buffered.
    /// </summary>
    /// <typeparam name="TClient">Client type resolved by callers.</typeparam>
    /// <typeparam name="TImplementation">Implementation registered for <typeparamref name="TClient"/>.</typeparam>
    /// <typeparam name="TOptions">Endpoint options bound from <paramref name="section"/>.</typeparam>
    /// <param name="services">Service collection to update.</param>
    /// <param name="section">Configuration section bound to <typeparamref name="TOptions"/>.</param>
    /// <returns>The HTTP client builder for <typeparamref name="TClient"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="section"/> is null.</exception>
    public static IHttpClientBuilder AddStreamingClient<TClient, TImplementation, TOptions>(
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

        builder.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TOptions>>().Value;
            return new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(options.Resilience.AttemptTimeoutSeconds),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ResponseDrainTimeout = TimeSpan.FromSeconds(2),
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            };
        });

        return builder;
    }

    /// <summary>
    /// Sends <paramref name="request"/> and returns the response before the body is read.
    /// The completion option is <see cref="HttpCompletionOption.ResponseHeadersRead"/>, so a large download is not buffered.
    /// The caller disposes the response, which disposes the body stream.
    /// </summary>
    /// <param name="client">Client that sends <paramref name="request"/>.</param>
    /// <param name="request">Request to send. This method does not dispose it.</param>
    /// <param name="cancellationToken">Token passed to <see cref="HttpClient.SendAsync(HttpRequestMessage, HttpCompletionOption, CancellationToken)"/>.</param>
    /// <returns>The response. Its content stream still holds the unread body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="request"/> is null.</exception>
    public static Task<HttpResponseMessage> SendStreamingAsync(
        this HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        return client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
