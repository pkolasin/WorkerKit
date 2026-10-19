using System.Reflection;
using System.Text;

namespace WorkerKit.Abstractions.Identity;

/// <summary>
/// Names and paths for one connector. Derived from the entry assembly, with an explicit override
/// when the product name, service name, or environment prefix should differ.
/// </summary>
public sealed record WorkerAppIdentity
{
    private WorkerAppIdentity(
        string name,
        string serviceName,
        string envPrefix,
        string version,
        Assembly applicationAssembly)
    {
        Name = name;
        ServiceName = serviceName;
        EnvPrefix = envPrefix;
        Version = version;
        ApplicationAssembly = applicationAssembly;
    }

    /// <summary>Product name. The default is the entry assembly product.</summary>
    public string Name { get; }

    /// <summary>Windows service name for the default instance. Named instances append <c>-{instance}</c>.</summary>
    public string ServiceName { get; }

    /// <summary>Prefix of <c>{prefix}_INSTANCE</c>, <c>{prefix}_INSTANCE_DIR</c>, and <c>{prefix}_INSTANCES_ROOT</c>.</summary>
    public string EnvPrefix { get; }

    /// <summary>Informational version of <see cref="ApplicationAssembly"/>, with a <c>+</c> suffix removed.</summary>
    public string Version { get; }

    /// <summary>Assembly that owns user secrets and <see cref="Version"/>.</summary>
    public Assembly ApplicationAssembly { get; }

    public string InstanceVariable => EnvPrefix + "_INSTANCE";

    public string InstanceDirectoryVariable => EnvPrefix + "_INSTANCE_DIR";

    public string InstancesRootVariable => EnvPrefix + "_INSTANCES_ROOT";

    public string WindowsInstancesRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Name, "instances");

    public string LinuxInstancesRoot => "/var/lib/" + ToKebabCase(Name);

    public string DefaultInstancesRoot => OperatingSystem.IsWindows() ? WindowsInstancesRoot : LinuxInstancesRoot;

    public string ServiceNameFor(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        return string.Equals(instanceName, "default", StringComparison.Ordinal)
            ? ServiceName
            : ServiceName + "-" + instanceName;
    }

    /// <summary>
    /// Uses <see cref="Assembly.GetEntryAssembly"/> when that assembly is the connector.
    /// A test host falls back to <paramref name="fallback"/>.
    /// </summary>
    public static WorkerAppIdentity FromEntryAssembly(Assembly fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        var entry = Assembly.GetEntryAssembly();
        if (entry is null || IsTestHost(entry))
        {
            return FromAssembly(fallback);
        }

        return FromAssembly(entry);
    }

    public static WorkerAppIdentity FromAssembly(Assembly applicationAssembly) =>
        Create(applicationAssembly);

    public static WorkerAppIdentity Create(
        Assembly applicationAssembly,
        string? name = null,
        string? serviceName = null,
        string? envPrefix = null)
    {
        ArgumentNullException.ThrowIfNull(applicationAssembly);
        var resolvedName = string.IsNullOrWhiteSpace(name) ? ReadProductName(applicationAssembly) : name.Trim();
        var resolvedService = string.IsNullOrWhiteSpace(serviceName) ? resolvedName : serviceName.Trim();
        var resolvedPrefix = string.IsNullOrWhiteSpace(envPrefix)
            ? DeriveEnvPrefix(resolvedName)
            : NormalizeEnvPrefix(envPrefix);
        return new WorkerAppIdentity(
            resolvedName,
            resolvedService,
            resolvedPrefix,
            ReadInformationalVersion(applicationAssembly),
            applicationAssembly);
    }

    public static string DeriveEnvPrefix(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var chars = name.Where(static character => char.IsAsciiLetterOrDigit(character)).ToArray();
        if (chars.Length == 0)
        {
            throw new ArgumentException(
                "Application name has no letters or digits to form an environment prefix.",
                nameof(name));
        }

        return new string(chars).ToUpperInvariant();
    }

    public static string NormalizeEnvPrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var trimmed = prefix.Trim();
        foreach (var character in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                throw new ArgumentException(
                    "Environment variable prefix must contain only letters and digits.",
                    nameof(prefix));
            }
        }

        return trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Lowercase path segment. <c>Contoso.Billing</c> becomes <c>contoso-billing</c>,
    /// <c>Northwind.Shipping</c> becomes <c>northwind-shipping</c>, and <c>HTTPServer</c> becomes <c>http-server</c>.
    /// </summary>
    public static string ToKebabCase(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var builder = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var current = name[index];
            if (current is '.' or '_' or ' ' or '-' or '/')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                continue;
            }

            if (char.IsUpper(current))
            {
                var previous = index > 0 ? name[index - 1] : '\0';
                var next = index + 1 < name.Length ? name[index + 1] : '\0';
                var boundary = char.IsLower(previous) || char.IsDigit(previous)
                    || (char.IsUpper(previous) && char.IsLower(next));
                if (boundary && builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        var kebab = builder.ToString().Trim('-');
        if (kebab.Length == 0)
        {
            throw new ArgumentException("Application name has no characters for a directory name.", nameof(name));
        }

        return kebab;
    }

    public static string ReadInformationalVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    private static string ReadProductName(Assembly assembly)
    {
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        if (!string.IsNullOrWhiteSpace(product))
        {
            return product.Trim();
        }

        var simple = assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(simple))
        {
            throw new InvalidOperationException("Assembly has no product name.");
        }

        const string hostSuffix = ".Host";
        if (simple.EndsWith(hostSuffix, StringComparison.Ordinal) && simple.Length > hostSuffix.Length)
        {
            simple = simple[..^hostSuffix.Length];
        }

        return simple;
    }

    private static bool IsTestHost(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        return string.IsNullOrEmpty(name)
            || name.Equals("testhost", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("testhost.", StringComparison.OrdinalIgnoreCase);
    }
}
