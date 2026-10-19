using System.ComponentModel.DataAnnotations;
using WorkerKit.Abstractions.Options;

namespace WorkerKit.Samples.OpenMeteo;

/// <summary>
/// Open-Meteo forecast settings. Latitude and longitude are optional.
/// When either is missing, the worker records a heartbeat and does not call the network.
/// </summary>
public sealed class OpenMeteoOptions : HttpClientEndpointOptions
{
    public const string SectionName = "OpenMeteo";

    public OpenMeteoOptions()
    {
        BaseAddress = "https://api.open-meteo.com/v1/";
        UserAgent = "WorkerKitExample/1.0";
    }

    public bool Enabled { get; set; } = true;

    [Range(1, 86_400, ErrorMessage = "OpenMeteo:PollIntervalSeconds must be between 1 and 86400.")]
    public int PollIntervalSeconds { get; set; } = 300;

    [Range(-90d, 90d, ErrorMessage = "OpenMeteo:Latitude must be between -90 and 90.")]
    public double? Latitude { get; set; }

    [Range(-180d, 180d, ErrorMessage = "OpenMeteo:Longitude must be between -180 and 180.")]
    public double? Longitude { get; set; }
}
