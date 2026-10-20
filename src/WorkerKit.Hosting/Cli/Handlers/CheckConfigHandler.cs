using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Configuration;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Data;
using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Cli.Handlers;

/// <summary>
/// Prints a redacted configuration summary for <c>check-config</c>, or the startup validation failures.
/// </summary>
public sealed class CheckConfigHandler
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly ICommandOutput _output;
    private readonly IInstanceContext _instance;
    private readonly WorkerAppIdentity _application;
    private readonly OptionsSectionCatalog _sections;

    /// <summary>Creates a handler from the built host.</summary>
    /// <param name="environment">Host environment whose name and content root are printed.</param>
    /// <param name="configuration">Configuration values printed in the summary.</param>
    /// <param name="services">Provider passed to <see cref="StartupValidation.GetFailures"/>.</param>
    /// <param name="output">Writer for the summary and for validation failures.</param>
    /// <param name="instance">Instance directories printed in the summary.</param>
    /// <param name="application">Application identity used for the telemetry service name.</param>
    /// <param name="sections">Catalog of option sections whose leaf keys are printed.</param>
    public CheckConfigHandler(
        IHostEnvironment environment,
        IConfiguration configuration,
        IServiceProvider services,
        ICommandOutput output,
        IInstanceContext instance,
        WorkerAppIdentity application,
        OptionsSectionCatalog sections)
    {
        _environment = environment;
        _configuration = configuration;
        _services = services;
        _output = output;
        _instance = instance;
        _application = application;
        _sections = sections;
    }

    /// <summary>
    /// Validates startup options. When validation fails, writes each failure and returns <see cref="ExitCodes.Error"/>.
    /// Otherwise writes the environment, instance directories, option keys (secrets masked), telemetry, health endpoints,
    /// and data-store readiness, then returns <see cref="ExitCodes.Success"/>.
    /// </summary>
    /// <returns><see cref="ExitCodes.Error"/> when validation fails; otherwise <see cref="ExitCodes.Success"/>.</returns>
    public int Execute()
    {
        var failures = StartupValidation.GetFailures(_services);
        if (failures.Count > 0)
        {
            _output.WriteError("Configuration is invalid:");
            foreach (var failure in failures)
            {
                _output.WriteError("  " + failure);
            }

            return ExitCodes.Error;
        }

        _output.WriteLine("Configuration is valid.");
        _output.WriteLine($"Environment: {_environment.EnvironmentName}");
        _output.WriteLine($"Content root: {_environment.ContentRootPath}");
        _output.WriteLine($"Instance: {_instance.InstanceName}");
        _output.WriteLine($"Install directory: {_instance.InstallDirectory}");
        _output.WriteLine($"Instance directory: {_instance.InstanceDirectory}");
        _output.WriteLine($"Log directory: {_instance.LogDirectory}");
        _output.WriteLine($"Data directory: {_instance.DataDirectory}");
        _output.WriteLine($"Temp directory: {_instance.TempDirectory}");
        var logPath = _configuration[$"{LogFileOptions.SectionName}:Path"] ?? "logs/worker-.json";
        _output.WriteLine($"Log file: {_instance.ResolvePath(logPath)}");
        foreach (var key in _sections.LeafKeys(_configuration))
        {
            if (OptionsSectionCatalog.IsSecret(key))
            {
                WriteMasked(key);
            }
            else
            {
                Write(key);
            }
        }

        var telemetry = TelemetryIdentity.Create(_configuration, _application, _environment.EnvironmentName, _instance);
        _output.WriteLine($"Telemetry service: {telemetry.ServiceName}");
        _output.WriteLine($"Telemetry version: {telemetry.ServiceVersion}");
        _output.WriteLine($"Telemetry instance: {telemetry.InstanceName}");
        var otlp = _configuration[OtlpLogExportSettings.EndpointKey];
        _output.WriteLine(string.IsNullOrWhiteSpace(otlp)
            ? "OTLP exporter: disabled"
            : $"OTLP exporter: {otlp.Trim()}");
        var listenUrl = _configuration[$"{HealthOptions.SectionName}:ListenUrl"]?.Trim();
        if (string.IsNullOrEmpty(listenUrl))
        {
            _output.WriteLine("Health endpoints: disabled");
        }
        else
        {
            var prefix = listenUrl.TrimEnd('/');
            _output.WriteLine($"Health endpoints: {prefix}/ (all), {prefix}/live (liveness), {prefix}/ready (readiness)");
        }

        if (string.Equals(_configuration[$"{DataOptions.SectionName}:Provider"], "Dapper", StringComparison.OrdinalIgnoreCase))
        {
            _output.WriteLine(
                "Data:Provider is Dapper. Register IDbConnectionFactory from ConfigureHost.");
            _output.WriteLine("Data store readiness: /ready runs SELECT 1 through IDbConnectionFactory.");
        }
        else
        {
            _output.WriteLine($"Data store readiness: not configured ({DataOptions.SectionName}:Provider is None).");
        }

        return ExitCodes.Success;
    }

    private void Write(string key)
    {
        var value = _configuration[key];
        _output.WriteLine($"{key} = {(string.IsNullOrEmpty(value) ? "(empty)" : value)}");
    }

    private void WriteMasked(string key)
    {
        var value = _configuration[key];
        _output.WriteLine($"{key} = {Mask(value)}");
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(empty)";
        }

        if (value.Length <= 4)
        {
            return "****";
        }

        return value[..2] + "****" + value[^2..];
    }
}
