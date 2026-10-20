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

    /// <summary>Name of the environment variable that selects the instance. The value is <see cref="EnvPrefix"/> plus <c>_INSTANCE</c>.</summary>
    public string InstanceVariable => EnvPrefix + "_INSTANCE";

    /// <summary>Name of the environment variable that selects the instance directory. The value is <see cref="EnvPrefix"/> plus <c>_INSTANCE_DIR</c>.</summary>
    public string InstanceDirectoryVariable => EnvPrefix + "_INSTANCE_DIR";

    /// <summary>Name of the environment variable that selects the instances root. The value is <see cref="EnvPrefix"/> plus <c>_INSTANCES_ROOT</c>.</summary>
    public string InstancesRootVariable => EnvPrefix + "_INSTANCES_ROOT";

    /// <summary>
    /// Default instances root on Windows:
    /// <see cref="Environment.SpecialFolder.CommonApplicationData"/>, <see cref="Name"/>, and <c>instances</c>.
    /// </summary>
    public string WindowsInstancesRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Name, "instances");

    /// <summary>Default instances root on Linux. The value is <c>/var/lib/</c> plus <see cref="ToKebabCase(string)"/> of <see cref="Name"/>.</summary>
    public string LinuxInstancesRoot => "/var/lib/" + ToKebabCase(Name);

    /// <summary>
    /// <see cref="WindowsInstancesRoot"/> when <see cref="OperatingSystem.IsWindows"/> is <see langword="true"/>;
    /// otherwise <see cref="LinuxInstancesRoot"/>.
    /// </summary>
    public string DefaultInstancesRoot => OperatingSystem.IsWindows() ? WindowsInstancesRoot : LinuxInstancesRoot;

    /// <summary>Windows service name for <paramref name="instanceName"/>.</summary>
    /// <param name="instanceName">Instance name. It cannot be <see langword="null"/>, empty, or white space.</param>
    /// <returns>
    /// <see cref="ServiceName"/> when <paramref name="instanceName"/> equals <c>default</c> by ordinal comparison;
    /// otherwise <see cref="ServiceName"/>, a hyphen, and <paramref name="instanceName"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="instanceName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="instanceName"/> is empty or white space.</exception>
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
    /// <param name="fallback">Assembly used when the entry assembly is missing or is a test host.</param>
    /// <returns>The identity of the entry assembly, or of <paramref name="fallback"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A null entry assembly uses <paramref name="fallback"/>.
    /// A test host has no simple name, the name <c>testhost</c>, or a name that starts with <c>testhost.</c>,
    /// compared without case.
    /// </remarks>
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

    /// <summary>Builds an identity from <paramref name="applicationAssembly"/> with no name overrides.</summary>
    /// <param name="applicationAssembly">Assembly that owns user secrets and the version.</param>
    /// <returns>The result of <see cref="Create(Assembly, string, string, string)"/> for <paramref name="applicationAssembly"/>.</returns>
    public static WorkerAppIdentity FromAssembly(Assembly applicationAssembly) =>
        Create(applicationAssembly);

    /// <summary>
    /// Builds an identity from <paramref name="applicationAssembly"/>.
    /// A null or white-space <paramref name="name"/> uses the assembly product name.
    /// A null or white-space <paramref name="serviceName"/> uses that product name.
    /// A null or white-space <paramref name="envPrefix"/> is derived from that product name; any other prefix is normalized.
    /// </summary>
    /// <param name="applicationAssembly">Assembly that owns user secrets and the version.</param>
    /// <param name="name">Product name override. Null or white space reads the name from the assembly.</param>
    /// <param name="serviceName">Service name override. Null or white space uses the product name.</param>
    /// <param name="envPrefix">Environment prefix override. Null or white space derives the prefix from the product name.</param>
    /// <returns>The identity for <paramref name="applicationAssembly"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="applicationAssembly"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <paramref name="name"/>, <paramref name="serviceName"/>, and <paramref name="envPrefix"/> default to <see langword="null"/>.
    /// A supplied name or service name is trimmed.
    /// When the assembly product attribute is absent, the assembly name is used and a trailing <c>.Host</c> is removed.
    /// The version comes from <see cref="ReadInformationalVersion(Assembly)"/>.
    /// </remarks>
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

    /// <summary>Builds an environment prefix from the ASCII letters and digits in <paramref name="name"/>.</summary>
    /// <param name="name">Application name.</param>
    /// <returns>Those characters, in order, converted with <see cref="string.ToUpperInvariant"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space, or it contains no ASCII letters or digits.</exception>
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

    /// <summary>Trims <paramref name="prefix"/> and converts it to uppercase.</summary>
    /// <param name="prefix">Environment variable prefix.</param>
    /// <returns><paramref name="prefix"/> trimmed and converted with <see cref="string.ToUpperInvariant"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="prefix"/> is empty or white space, or a character that remains after trimming is not an ASCII letter or digit.
    /// </exception>
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
    /// <param name="name">Application name.</param>
    /// <returns>The lowercase path segment described above.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or white space, or it contains no character that remains in the path segment.
    /// </exception>
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

    /// <summary>
    /// Reads <see cref="AssemblyInformationalVersionAttribute"/> from <paramref name="assembly"/>.
    /// A <c>+</c> that is not the first character, and everything after it, is removed.
    /// When that attribute is missing or white space, the assembly version is used.
    /// </summary>
    /// <param name="assembly">Assembly whose version is read.</param>
    /// <returns>
    /// The informational version, with a <c>+</c> suffix removed when <c>+</c> is not the first character.
    /// When the attribute is missing or white space, the assembly version, or <c>unknown</c> when the assembly has no version.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
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
