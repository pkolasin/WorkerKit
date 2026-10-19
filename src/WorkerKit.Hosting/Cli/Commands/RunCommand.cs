using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting;

using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Cli.Commands;

internal static class RunCommand
{
    public static Command Create(WorkerHostPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var command = new Command("run", "Start the worker host and block until shutdown.");
        // Host configuration switches such as --Worker:PollIntervalSeconds 30 are forwarded to the generic host.
        command.TreatUnmatchedTokensAsErrors = false;
        command.SetAction((_, cancellationToken) => ExecuteAsync(plan, cancellationToken));
        return command;
    }

    public static async Task<int> ExecuteAsync(WorkerHostPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        try
        {
            using var host = WorkerHostBuilder.Create(
                plan.Args,
                enableTelemetry: true,
                identity: plan.Identity,
                configureHost: plan.HasConfiguration ? plan.Apply : null,
                operatingSystemIntegration: plan.OperatingSystemIntegration).Build();
            var instance = host.Services.GetRequiredService<IInstanceContext>();
            var requireLock = host.Services.GetRequiredService<IOptions<InstanceOptions>>().Value.RequireLock;
            // Disposed before the host (reverse order of the using declarations). Connector commands and check-config do not lock.
            using var instanceLock = requireLock ? InstanceLock.Acquire(instance) : null;
            var application = host.Services.GetRequiredService<WorkerAppIdentity>();
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(application.Name);
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStarted.Register(() =>
                logger.LogInformation("Worker host started for instance {InstanceName}", instance.InstanceName));
            lifetime.ApplicationStopping.Register(() => logger.LogInformation("Worker host is shutting down"));
            lifetime.ApplicationStopped.Register(() => logger.LogInformation("Worker host stopped"));

            try
            {
                await host.RunAsync(cancellationToken).ConfigureAwait(false);
                return ExitCodes.Success;
            }
            catch (OperationCanceledException)
            {
                return ExitCodes.Success;
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Worker host terminated");
                WriteError(exception);
                return ExitCodes.Error;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            WriteError(exception);
            return ExitCodes.Error;
        }
    }

    private static void WriteError(Exception exception)
    {
        if (exception is OptionsValidationException options)
        {
            Console.Error.WriteLine("Configuration is invalid:");
            foreach (var failure in options.Failures)
            {
                Console.Error.WriteLine("  " + failure);
            }

            return;
        }

        Console.Error.WriteLine(exception.Message);
    }
}
