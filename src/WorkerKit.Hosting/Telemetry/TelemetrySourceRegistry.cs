namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// ActivitySource and Meter names a connector added with <c>AddTelemetrySource</c>.
/// <c>AddWorkerDefaults</c> reads this after the connector delegates run.
/// </summary>
public sealed class TelemetrySourceRegistry
{
    private readonly List<string> _names = [];

    public IReadOnlyList<string> Names => _names;

    public void Add(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_names.Contains(name, StringComparer.Ordinal))
        {
            return;
        }

        _names.Add(name);
    }

    public static TelemetrySourceRegistry GetOrCreate(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var existing = Find(services);
        if (existing is not null)
        {
            return existing;
        }

        var created = new TelemetrySourceRegistry();
        services.AddSingleton(created);
        return created;
    }

    public static TelemetrySourceRegistry? Find(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(TelemetrySourceRegistry)
                && descriptor.ImplementationInstance is TelemetrySourceRegistry registry)
            {
                return registry;
            }
        }

        return null;
    }
}
