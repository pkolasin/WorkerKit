using WorkerKit.Abstractions.Identity;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Resource identity shared by traces, metrics, and the Serilog OTLP sink.
/// <see cref="InstanceName"/> is <c>service.instance.id</c>. The machine name stays in <c>host.name</c>.
/// </summary>
public sealed record TelemetryIdentity(
    string ServiceName,
    string ServiceVersion,
    string InstanceName,
    string HostName,
    string EnvironmentName)
{
    public const string ServiceNameKey = "Telemetry:ServiceName";

    /// <summary>Informational version of this library assembly. Resource attribute <c>workerkit.version</c>.</summary>
    public string LibraryVersion { get; init; } = ReadServiceVersion();

    public static TelemetryIdentity Create(
        IConfiguration configuration,
        string applicationName,
        string environmentName,
        IInstanceContext instance)
        => Create(configuration, applicationName, ReadServiceVersion(), environmentName, instance);

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
    public static string ReadServiceVersion() =>
        WorkerAppIdentity.ReadInformationalVersion(typeof(TelemetryIdentity).Assembly);

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
