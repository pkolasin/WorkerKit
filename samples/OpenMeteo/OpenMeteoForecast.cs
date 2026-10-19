using System.Text.Json.Serialization;

namespace WorkerKit.Samples.OpenMeteo;

public sealed class OpenMeteoForecast
{
    public OpenMeteoCurrent? Current { get; set; }
}

public sealed class OpenMeteoCurrent
{
    public string? Time { get; set; }

    [JsonPropertyName("temperature_2m")]
    public double? TemperatureC { get; set; }
}
