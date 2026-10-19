using ExampleHost.Composition;
using Microsoft.Extensions.Hosting;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting;

namespace ExampleHost.Tests;

/// <summary>
/// Example-test host. The framework no longer registers the sample when no connector is given.
/// </summary>
internal static class ExampleHosts
{
    public static WorkerAppIdentity Identity { get; } =
        WorkerAppIdentity.FromAssembly(typeof(ExampleConnectorExtensions).Assembly);

    public static HostApplicationBuilder Create(
        string[] args,
        string? contentRoot = null,
        string? environmentName = null,
        Action<HostApplicationBuilder>? configure = null,
        bool enableTelemetry = false,
        WorkerAppIdentity? identity = null,
        Action<IHostApplicationBuilder>? configureHost = null)
    {
        return WorkerHostBuilder.Create(
            args,
            contentRoot,
            environmentName,
            configure,
            enableTelemetry,
            identity ?? Identity,
            builder =>
            {
                builder.AddExampleConnector();
                configureHost?.Invoke(builder);
            });
    }
}
