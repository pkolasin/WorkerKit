namespace WorkerKit.Samples.OpenMeteo;

public interface IOpenMeteoClient
{
    Task<OpenMeteoForecast> GetCurrentTemperatureAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
