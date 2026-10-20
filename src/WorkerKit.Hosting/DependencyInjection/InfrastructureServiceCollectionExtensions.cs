using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkerKit.Hosting.Health;
using WorkerKit.Hosting.Telemetry;
using WorkerKit.Hosting.Validation;

using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Workers;
namespace WorkerKit.Hosting.DependencyInjection;

/// <summary>Registers the framework services that every worker host receives.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers framework options, health checks, and <see cref="WorkerContext"/>.
    /// Connector clients, workers, and stores are added from <c>ConfigureHost</c>.
    /// </summary>
    /// <param name="services">Service collection to update.</param>
    /// <param name="configuration">Configuration that supplies the framework option sections.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configuration"/> is null.</exception>
    /// <remarks>Registers <see cref="TimeProvider.System"/> as the <see cref="TimeProvider"/> singleton used by the host.</remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddValidatedOptions<HealthOptions>(configuration.GetSection(HealthOptions.SectionName));
        services.AddSingleton<IValidateOptions<HealthOptions>, HealthOptionsValidator>();
        services.AddValidatedOptions<DataOptions>(configuration.GetSection(DataOptions.SectionName));
        services.AddSingleton<IValidateOptions<DataOptions>, DataOptionsValidator>();
        services.AddValidatedOptions<LogFileOptions>(configuration.GetSection(LogFileOptions.SectionName));
        services.AddValidatedOptions<InstanceOptions>(configuration.GetSection(InstanceOptions.SectionName));
        services.AddSingleton<IValidateOptions<InstanceOptions>, InstanceOptionsValidator>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWorkerHeartbeat, WorkerHeartbeatRegistry>();
        // run and a connector command that enables telemetry replace this with the OpenTelemetry observer.
        services.AddSingleton<IWorkerIterationObserver, NoOpWorkerIterationObserver>();
        services.AddSingleton<WorkerContext>(static serviceProvider => new WorkerContext(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            serviceProvider.GetRequiredService<IWorkerHeartbeat>(),
            serviceProvider.GetRequiredService<IWorkerIterationObserver>(),
            serviceProvider.GetRequiredService<ILoggerFactory>(),
            serviceProvider.GetService<WorkerTelemetry>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        services.AddHostedService<HealthEndpointWorker>();
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("process is running"), tags: ["live"])
            .AddCheck<WorkerHeartbeatHealthCheck>("workers", tags: ["live", "ready"])
            .AddCheck<InstanceDirectoriesHealthCheck>("instance-dirs", tags: ["ready"])
            .AddCheck<DataStoreHealthCheck>("data-store", tags: ["ready"]);
        services.AddSingleton<IHealthCheckPublisher, LoggingHealthCheckPublisher>();
        services.AddOptions<HealthCheckPublisherOptions>()
            .Configure<IOptions<HealthOptions>>((publisher, health) =>
            {
                publisher.Delay = TimeSpan.FromSeconds(2);
                publisher.Period = TimeSpan.FromSeconds(Math.Max(1, health.Value.PublisherPeriodSeconds));
                publisher.Predicate = _ => true;
            });

        return services;
    }
}
