namespace WorkerKit.Hosting.Configuration;

/// <summary>
/// Section paths recorded when options are bound. <c>check-config</c> prints those sections
/// instead of a fixed key list. The same instance is stored on the service collection so
/// registration and the built provider share it.
/// </summary>
public sealed class OptionsSectionCatalog
{
    private readonly List<string> _sections = [];
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Section paths passed to <see cref="Remember"/>, in first-seen order.</summary>
    public IReadOnlyList<string> Sections => _sections;

    /// <summary>Records <paramref name="sectionPath"/> when it has not already been recorded. Comparison is case-insensitive.</summary>
    /// <param name="sectionPath">Configuration section path, such as a bound options section.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sectionPath"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sectionPath"/> is empty or whitespace.</exception>
    public void Remember(string sectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        if (_seen.Add(sectionPath))
        {
            _sections.Add(sectionPath);
        }
    }

    /// <summary>
    /// Returns the catalog stored as a singleton on <paramref name="services"/>, or adds one and returns it.
    /// </summary>
    /// <param name="services">Service collection that holds the catalog.</param>
    /// <returns>The existing or newly registered catalog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static OptionsSectionCatalog GetOrCreate(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(OptionsSectionCatalog)
                && descriptor.ImplementationInstance is OptionsSectionCatalog catalog)
            {
                return catalog;
            }
        }

        var created = new OptionsSectionCatalog();
        services.AddSingleton(created);
        return created;
    }

    /// <summary>
    /// Lists leaf keys under each remembered section. A leaf has no children and a non-null value.
    /// Keys from one section are sorted ordinally before the next section is appended.
    /// </summary>
    /// <param name="configuration">Configuration to read.</param>
    /// <returns>Leaf key paths that have a value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    public IReadOnlyList<string> LeafKeys(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var keys = new List<string>();
        foreach (var sectionPath in _sections)
        {
            var found = new List<string>();
            Collect(configuration.GetSection(sectionPath), found);
            found.Sort(StringComparer.Ordinal);
            keys.AddRange(found);
        }

        return keys;
    }

    /// <summary>
    /// A key is secret when its last segment contains ConnectionString, Password, Secret, or Token.
    /// </summary>
    /// <param name="key">Configuration key. The last <c>:</c> segment is tested.</param>
    /// <returns>True when the last segment contains ConnectionString, Password, Secret, or Token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    public static bool IsSecret(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var segment = key.Split(':')[^1];
        return segment.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || segment.Contains("Token", StringComparison.OrdinalIgnoreCase);
    }

    private static void Collect(IConfigurationSection section, List<string> keys)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            if (!string.IsNullOrEmpty(section.Path) && section.Value is not null)
            {
                keys.Add(section.Path);
            }

            return;
        }

        foreach (var child in children)
        {
            Collect(child, keys);
        }
    }
}
