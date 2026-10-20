namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// ActivitySource and Meter names a connector added with <c>AddTelemetrySource</c>.
/// <c>AddWorkerDefaults</c> reads this after the connector delegates run.
/// </summary>
public sealed class TelemetrySourceRegistry
{
    private readonly List<string> _names = [];

    /// <summary>Names passed to <see cref="Add"/>, in insertion order. A duplicate ordinal name is stored once.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>
    /// Adds <paramref name="name"/> when that ordinal value is not already present.
    /// The stored text is not trimmed.
    /// </summary>
    /// <param name="name">Activity source and meter name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public void Add(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_names.Contains(name, StringComparer.Ordinal))
        {
            return;
        }

        _names.Add(name);
    }

    /// <summary>Returns the registry already stored as a singleton instance, or adds one and returns it.</summary>
    /// <param name="services">Service collection that owns the registry.</param>
    /// <returns>The existing or newly added registry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
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

    /// <summary>
    /// Returns the registry stored as a singleton instance.
    /// A registration that is not an instance, such as a factory, is ignored. The result is null when none is stored.
    /// </summary>
    /// <param name="services">Service collection to scan.</param>
    /// <returns>The registry instance, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
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
