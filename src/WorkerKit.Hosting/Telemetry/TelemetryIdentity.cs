using WorkerKit.Abstractions.Identity;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Resource identity shared by traces, metrics, and the Serilog OTLP sink.
/// <see cref="InstanceName"/> is <c>service.instance.id</c>. The machine name stays in <c>host.name</c>.
/// </summary>
/// <param name="ServiceName">Resource attribute <c>service.name</c>. <see cref="ServiceNameKey"/> replaces the application name when it is set.</param>
/// <param name="ServiceVersion">Resource attribute <c>service.version</c>.</param>
/// <param name="InstanceName">Resource attribute <c>service.instance.id</c>.</param>
/// <param name="HostName">Resource attribute <c>host.name</c>. <see cref="Create(IConfiguration, string, string, IInstanceContext)"/> sets it from <see cref="Environment.MachineName"/>.</param>
/// <param name="EnvironmentName">Resource attribute <c>deployment.environment.name</c>.</param>
public sealed record TelemetryIdentity(
    string ServiceName,
    string ServiceVersion,
    string InstanceName,
    string HostName,
    string EnvironmentName)
{
    /// <summary>Configuration key <c>Telemetry:ServiceName</c>. A non-white-space value replaces the application name.</summary>
    public const string ServiceNameKey = "Telemetry:ServiceName";

    /// <summary>Informational version of this library assembly. Resource attribute <c>workerkit.version</c>.</summary>
    public string LibraryVersion { get; init; } = ReadServiceVersion();

    /// <summary>
    /// Builds an identity whose <see cref="ServiceVersion"/> is <see cref="ReadServiceVersion"/>.
    /// <see cref="ServiceNameKey"/> replaces <paramref name="applicationName"/> when that value is non-white-space.
    /// </summary>
    /// <param name="configuration">Configuration read for <see cref="ServiceNameKey"/>.</param>
    /// <param name="applicationName">Service name used when <see cref="ServiceNameKey"/> is empty.</param>
    /// <param name="environmentName">Stored as <see cref="EnvironmentName"/>.</param>
    /// <param name="instance">Supplies <see cref="IInstanceContext.InstanceName"/>.</param>
    /// <returns>The resource identity. <see cref="HostName"/> is <see cref="Environment.MachineName"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> or <paramref name="instance"/> is null.</exception>
    public static TelemetryIdentity Create(
        IConfiguration configuration,
        string applicationName,
        string environmentName,
        IInstanceContext instance)
        => Create(configuration, applicationName, ReadServiceVersion(), environmentName, instance);

    /// <summary>
    /// Builds an identity from <paramref name="application"/>.
    /// <see cref="ServiceName"/> is <see cref="WorkerAppIdentity.Name"/> unless <see cref="ServiceNameKey"/> is set.
    /// <see cref="ServiceVersion"/> is <see cref="WorkerAppIdentity.Version"/>.
    /// </summary>
    /// <param name="configuration">Configuration read for <see cref="ServiceNameKey"/>.</param>
    /// <param name="application">Application name and version.</param>
    /// <param name="environmentName">Stored as <see cref="EnvironmentName"/>.</param>
    /// <param name="instance">Supplies <see cref="IInstanceContext.InstanceName"/>.</param>
    /// <returns>The resource identity. <see cref="HostName"/> is <see cref="Environment.MachineName"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="application"/>, <paramref name="configuration"/>, or <paramref name="instance"/> is null.</exception>
    public static TelemetryIdentity Create(
        IConfiguration configuration,
        WorkerAppIdentity application,
        string environmentName,
        IInstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(application);
        return Create(configuration, application.Name, application.Version, environmentName, instance);
    }

    /// <summary>
    /// Informational version of the WorkerKit.Hosting assembly.
    /// <c>service.version</c> on a host uses <see cref="WorkerAppIdentity.Version"/> instead.
    /// </summary>
    /// <returns>
    /// That informational version with a <c>+</c> suffix removed.
    /// When the attribute is missing, the assembly version, or <c>unknown</c>.
    /// </returns>
    public static string ReadServiceVersion() =>
        WorkerAppIdentity.ReadInformationalVersion(typeof(TelemetryIdentity).Assembly);

    /// <summary>
    /// Copies this identity into Serilog resource attributes:
    /// <c>service.name</c>, <c>service.version</c>, <c>workerkit.version</c>, <c>service.instance.id</c>, <c>host.name</c>, and <c>deployment.environment.name</c>.
    /// </summary>
    /// <returns>A new dictionary that compares keys ordinally. <c>workerkit.version</c> is <see cref="LibraryVersion"/>.</returns>
    public Dictionary<string, object> ToSerilogAttributes()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["service.name"] = ServiceName,
            ["service.version"] = ServiceVersion,
            ["workerkit.version"] = LibraryVersion,
            ["service.instance.id"] = InstanceName,
            ["host.name"] = HostName,
            ["deployment.environment.name"] = EnvironmentName,
        };
    }

    private static TelemetryIdentity Create(
        IConfiguration configuration,
        string applicationName,
        string serviceVersion,
        string environmentName,
        IInstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(instance);
        var configured = configuration[ServiceNameKey];
        var serviceName = string.IsNullOrWhiteSpace(configured)
            ? applicationName
            : configured.Trim();
        return new TelemetryIdentity(
            serviceName,
            serviceVersion,
            instance.InstanceName,
            Environment.MachineName,
            environmentName);
    }
}
