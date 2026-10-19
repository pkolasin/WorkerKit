using System.Net.Http.Json;
using System.Text.Json;

namespace WorkerKit.Samples.OpenMeteo;

/// <summary>
/// Typed Open-Meteo client. Base address and resilience come from <see cref="IHttpClientFactory"/>.
/// </summary>
public sealed class OpenMeteoClient : IOpenMeteoClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(HttpClient httpClient, ILogger<OpenMeteoClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
        {
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }
    }

    public async Task<OpenMeteoForecast> GetCurrentTemperatureAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var path = "forecast?latitude="
            + Uri.EscapeDataString(latitude.ToString(System.Globalization.CultureInfo.InvariantCulture))
            + "&longitude="
            + Uri.EscapeDataString(longitude.ToString(System.Globalization.CultureInfo.InvariantCulture))
            + "&current=temperature_2m";
        _logger.LogDebug("Open-Meteo GET {Path}", path);
        using var response = await _httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (body.Length > 500)
            {
                body = body[..500];
            }

            throw new HttpRequestException(
                $"Open-Meteo returned {(int)response.StatusCode} ({response.StatusCode}). {body}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var forecast = await response.Content.ReadFromJsonAsync<OpenMeteoForecast>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (forecast?.Current?.TemperatureC is null)
        {
            throw new HttpRequestException("Open-Meteo returned no current temperature_2m.");
        }

        return forecast;
    }
}
