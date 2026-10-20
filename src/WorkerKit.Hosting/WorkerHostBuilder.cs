using System.Reflection;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting.DependencyInjection;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting;

/// <summary>Builds the generic host used by <c>run</c>, <c>check-config</c>, and connector commands.</summary>
public static class WorkerHostBuilder
{
    /// <summary>
    /// Resolves the instance, loads instance configuration, and registers framework services.
    /// The content root is the install directory.
    /// </summary>
    /// <param name="args">Arguments used for instance resolution and host configuration.</param>
    /// <param name="contentRoot">Install directory. When null, the directory is <see cref="AppContext.BaseDirectory"/>.</param>
    /// <param name="environmentName">Host environment. When null or empty, the host builder settings leave it unset.</param>
    /// <param name="configure">Callback invoked after <paramref name="configureHost"/> and before telemetry.</param>
    /// <param name="enableTelemetry">When true, registers the worker telemetry pipeline. The parameter default is false.</param>
    /// <param name="identity">Application identity. When null, <see cref="AppIdentity.Current"/> is used.</param>
    /// <param name="configureHost">Callback invoked after framework services and before <paramref name="configure"/>.</param>
    /// <param name="operatingSystemIntegration">When true, registers Windows Service and systemd integration. The parameter default is true.</param>
    /// <returns>The host builder. The host is not built or started.</returns>
    /// <remarks>
    /// <see cref="InstanceLocator.Resolve(string[], Func{string, string?}, string, WorkerAppIdentity)"/> runs before the host exists,
    /// because that choice decides which configuration files are loaded.
    /// Telemetry is registered only when <paramref name="enableTelemetry"/> is true, after the configuration callbacks.
    /// </remarks>
    public static HostApplicationBuilder Create(
        string[] args,
        string? contentRoot = null,
        string? environmentName = null,
        Action<HostApplicationBuilder>? configure = null,
        bool enableTelemetry = false,
        WorkerAppIdentity? identity = null,
        Action<IHostApplicationBuilder>? configureHost = null,
        bool operatingSystemIntegration = true)
    {
        // Content root is the install directory, never the process working directory.
        // contentRoot stays for tests. Instance resolution runs before the host is built
        // because it decides which configuration files are loaded.
        identity ??= AppIdentity.Current;
        var installDir = contentRoot ?? AppContext.BaseDirectory;
        var location = InstanceLocator.Resolve(args, Environment.GetEnvironmentVariable, installDir, identity);
        var settings = new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = installDir,
            ApplicationName = identity.Name,
        };
        if (!string.IsNullOrEmpty(environmentName))
        {
            settings.EnvironmentName = environmentName;
        }

        // Qualified so a nested type or import named Host cannot hide Microsoft.Extensions.Hosting.Host.
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(settings);
        if (!location.IsDefault)
        {
            // The default instance lives in the install directory, whose appsettings are already loaded.
            AddInstanceJson(builder, location.Directory);
        }

        if (builder.Environment.IsDevelopment())
        {
            // ApplicationName is the product identity, not the entry assembly name.
            // CreateApplicationBuilder loads secrets with Assembly.Load(ApplicationName),
            // which misses UserSecretsId. Add them from the entry assembly after the JSON
            // files and before environment variables and the command line.
            AddDevelopmentUserSecrets(builder, identity.ApplicationAssembly);
        }

        var instance = InstanceEnvironment.Create(location, installDir, builder.Configuration);
        instance.EnsureDirectories();
        builder.Services.AddSingleton(identity);
        builder.Services.AddSingleton<IInstanceContext>(instance);
        builder.RememberInstance(instance);

        // Both calls are safe on every OS. Each one activates only when the process
        // is actually hosted as that kind of service (Windows SCM, or systemd with Type=notify).
        // Tests pass false so the host does not register those lifetimes.
        if (operatingSystemIntegration)
        {
            builder.Services.AddWindowsService(options => options.ServiceName = identity.ServiceNameFor(instance.InstanceName));
            builder.Services.AddSystemd();
        }
        builder.Services.Configure<HostOptions>(builder.Configuration.GetSection(nameof(HostOptions)));

        // Serilog, then health and the rest of the framework, then connector delegates, then telemetry.
        builder.AddWorkerSerilog(instance, enableTelemetry, identity);
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<ICommandOutput, ConsoleCommandOutput>();
        builder.Services.AddSingleton<VersionHandler>();
        builder.Services.AddSingleton<CheckConfigHandler>();
        // No sample connector. The application passes its own delegates.
        configureHost?.Invoke(builder);

        configure?.Invoke(builder);
        if (enableTelemetry)
        {
            // After configure so an in-memory OTEL_EXPORTER_OTLP_ENDPOINT is visible.
            // help, version, and check-config leave this off. Connector commands opt in.
            builder.AddWorkerDefaults(instance, identity);
        }

        return builder;
    }

    private static void AddInstanceJson(HostApplicationBuilder builder, string instanceDirectory)
    {
        // PhysicalFileProvider requires the directory to exist.
        Directory.CreateDirectory(instanceDirectory);
        var sources = builder.Configuration.Sources;
        var insertAt = sources.Count;
        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index] is JsonConfigurationSource { Path: { } path }
                && Path.GetFileName(path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            {
                insertAt = index + 1;
            }
        }

        var fileProvider = new PhysicalFileProvider(instanceDirectory);
        var environmentName = builder.Environment.EnvironmentName;
        builder.Configuration.AddJsonFile(fileProvider, "appsettings.json", optional: true, reloadOnChange: true);
        builder.Configuration.AddJsonFile(fileProvider, $"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);

        var environmentSource = sources[^1];
        var instanceSource = sources[^2];
        sources.RemoveAt(sources.Count - 1);
        sources.RemoveAt(sources.Count - 1);
        sources.Insert(insertAt, instanceSource);
        sources.Insert(insertAt + 1, environmentSource);
    }

    private static void AddDevelopmentUserSecrets(HostApplicationBuilder builder, Assembly applicationAssembly)
    {
        var sources = builder.Configuration.Sources;
        // The builder also adds a DOTNET_ environment source and an early command-line
        // source before the JSON files. Secrets belong before the later, unprefixed
        // environment source, which is followed by the winning command-line source.
        var insertAt = sources.Count;
        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index] is EnvironmentVariablesConfigurationSource { Prefix: null or "" })
            {
                insertAt = index;
                break;
            }
        }

        builder.Configuration.AddUserSecrets(applicationAssembly, optional: true, reloadOnChange: true);
        if (insertAt >= sources.Count - 1)
        {
            return;
        }

        var secrets = sources[^1];
        sources.RemoveAt(sources.Count - 1);
        sources.Insert(insertAt, secrets);
    }
}
