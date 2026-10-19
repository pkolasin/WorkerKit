using WorkerKit.Hosting.Workers;

namespace WorkerKit.Samples.OpenMeteo;

/// <summary>
/// Polls the current temperature. With latitude or longitude unset it logs and does not call the network.
/// That idle iteration is still a successful heartbeat.
/// </summary>
public sealed class ForecastWorker : PeriodicWorker
{
    public const string WorkerNameValue = "open-meteo";

    private readonly IOptionsMonitor<OpenMeteoOptions> _options;

    public ForecastWorker(WorkerContext context, IOptionsMonitor<OpenMeteoOptions> options)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    protected override string WorkerName => WorkerNameValue;

    protected override TimeSpan Interval =>
        TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.PollIntervalSeconds));

    protected override bool IsEnabled => _options.CurrentValue.Enabled;

    protected override void LogDisabled()
    {
        Logger.LogInformation("Open-Meteo worker is disabled (OpenMeteo:Enabled = false)");
    }

    protected override async Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (options.Latitude is not double latitude || options.Longitude is not double longitude)
        {
            Logger.LogInformation(
                "Open-Meteo heartbeat. Set OpenMeteo:Latitude and OpenMeteo:Longitude to request the current temperature");
            return 0;
        }

        var client = services.GetRequiredService<IOpenMeteoClient>();
        var forecast = await client.GetCurrentTemperatureAsync(latitude, longitude, cancellationToken)
            .ConfigureAwait(false);
        Logger.LogInformation(
            "Open-Meteo temperature at {Latitude}, {Longitude} is {TemperatureC} C ({Time})",
            latitude,
            longitude,
            forecast.Current!.TemperatureC,
            forecast.Current.Time);
        return 1;
    }
}
