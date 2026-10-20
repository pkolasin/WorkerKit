using System.Net;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Health;

/// <summary>
/// Optional HTTP health endpoint on a loopback prefix. It does nothing unless <c>Health:ListenUrl</c> is set.
/// <c>{prefix}</c> reports every check, <c>{prefix}live</c> reports the <c>live</c> tag, and <c>{prefix}ready</c> reports <c>ready</c>.
/// </summary>
public sealed class HealthEndpointWorker : BackgroundService
{
    private readonly HealthCheckService _healthChecks;
    private readonly IOptionsMonitor<HealthOptions> _options;
    private readonly IInstanceContext _instance;
    private readonly ILogger<HealthEndpointWorker> _logger;

    /// <summary>Stores the services the listen loop uses.</summary>
    /// <param name="healthChecks">Runs the combined, <c>live</c>, or <c>ready</c> checks for each request.</param>
    /// <param name="options">Supplies <see cref="HealthOptions.ListenUrl"/>.</param>
    /// <param name="instance">Instance name written into each JSON body.</param>
    /// <param name="logger">Logs when the endpoint is disabled, listening, failing, or stopped.</param>
    public HealthEndpointWorker(
        HealthCheckService healthChecks,
        IOptionsMonitor<HealthOptions> options,
        IInstanceContext instance,
        ILogger<HealthEndpointWorker> logger)
    {
        _healthChecks = healthChecks;
        _options = options;
        _instance = instance;
        _logger = logger;
    }

    /// <summary>
    /// Serves <see cref="HealthOptions.ListenUrl"/> until <paramref name="stoppingToken"/> is cancelled.
    /// An empty URL logs once and returns. A prefix that fails to start logs the error and returns.
    /// </summary>
    /// <param name="stoppingToken">Cancels the accept loop and closes the listener.</param>
    /// <returns>A task that completes when the endpoint is disabled, fails to bind, or the host stops it.</returns>
    /// <remarks>
    /// The prefix path runs every check, <c>live</c> runs the live tag, and <c>ready</c> runs the ready tag.
    /// Any other path returns 404. A request that throws, other than cancellation, is logged and answered with status 500.
    /// The JSON status code comes from <see cref="HealthResponses.StatusCode(string, HealthStatus)"/>.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listenUrl = _options.CurrentValue.ListenUrl?.Trim();
        if (string.IsNullOrEmpty(listenUrl))
        {
            _logger.LogInformation("HTTP health endpoint is disabled. Set Health:ListenUrl to enable it");
            return;
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add(listenUrl);
        try
        {
            listener.Start();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "HTTP health endpoint failed to listen on {ListenUrl}", listenUrl);
            return;
        }

        _logger.LogInformation("HTTP health endpoint listening on {ListenUrl}", listenUrl);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (HttpListenerException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await WriteResponseAsync(listenUrl, context, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            listener.Close();
            _logger.LogInformation("HTTP health endpoint stopped");
        }
    }

    private async Task WriteResponseAsync(string listenUrl, HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            var route = HealthResponses.Match(listenUrl, path);
            if (route is null)
            {
                await WriteBodyAsync(context, 404, """{"status":"NotFound"}""", cancellationToken).ConfigureAwait(false);
                return;
            }

            var report = route switch
            {
                "live" => await _healthChecks.CheckHealthAsync(
                    static registration => registration.Tags.Contains("live"),
                    cancellationToken).ConfigureAwait(false),
                "ready" => await _healthChecks.CheckHealthAsync(
                    static registration => registration.Tags.Contains("ready"),
                    cancellationToken).ConfigureAwait(false),
                _ => await _healthChecks.CheckHealthAsync(cancellationToken).ConfigureAwait(false),
            };
            var json = HealthResponses.Serialize(report, _instance.InstanceName, HealthResponses.HostVersion());
            await WriteBodyAsync(context, HealthResponses.StatusCode(route, report.Status), json, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "HTTP health request failed");
            context.Response.StatusCode = 500;
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static async Task WriteBodyAsync(
        HttpListenerContext context,
        int statusCode,
        string json,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }
}
