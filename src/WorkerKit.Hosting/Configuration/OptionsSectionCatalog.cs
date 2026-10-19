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

    public IReadOnlyList<string> Sections => _sections;

    public void Remember(string sectionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        if (_seen.Add(sectionPath))
        {
            _sections.Add(sectionPath);
        }
    }

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
