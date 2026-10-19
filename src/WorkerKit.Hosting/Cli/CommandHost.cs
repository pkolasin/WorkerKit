using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Telemetry;

namespace WorkerKit.Hosting.Cli;

internal static class CommandHost
{
    public static async Task<int> InvokeAsync(
        string[] args,
        Func<IServiceProvider, CancellationToken, Task<int>> action,
        CancellationToken cancellationToken,
        bool validate,
        bool enableTelemetry = false,
        WorkerAppIdentity? identity = null,
        Action<IHostApplicationBuilder>? configureHost = null,
        bool operatingSystemIntegration = true)
    {
        try
        {
            using var host = WorkerHostBuilder.Create(
                args,
                enableTelemetry: enableTelemetry,
                identity: identity,
                configureHost: configureHost,
                operatingSystemIntegration: operatingSystemIntegration).Build();
            var telemetryStarted = false;
            try
            {
                if (validate)
                {
                    var failures = StartupValidation.GetFailures(host.Services);
                    if (failures.Count > 0)
                    {
                        Console.Error.WriteLine("Configuration is invalid:");
                        foreach (var failure in failures)
                        {
                            Console.Error.WriteLine("  " + failure);
                        }

                        return ExitCodes.Error;
                    }
                }

                if (enableTelemetry)
                {
                    // The host is not started, so the OpenTelemetry hosted service never builds the providers.
                    // Build them before the command records spans, then flush before this host is disposed.
                    TelemetryShutdown.EnsureStarted(host.Services);
                    telemetryStarted = true;
                }

                await using var scope = host.Services.CreateAsyncScope();
                return await action(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (telemetryStarted)
                {
                    TelemetryShutdown.Flush(host.Services);
                    FlushLogs(host.Services);
                }
            }
        }
        catch (OptionsValidationException exception)
        {
            Console.Error.WriteLine("Configuration is invalid:");
            foreach (var failure in exception.Failures)
            {
                Console.Error.WriteLine("  " + failure);
            }

            return ExitCodes.Error;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            return ExitCodes.Error;
        }
    }

    private static void FlushLogs(IServiceProvider services)
    {
        // Disposing the Serilog logger flushes the OTLP log sink. Dispose is idempotent, and the
        // host disposes the same instance again when the service provider shuts down.
        if (services.GetService<Serilog.ILogger>() is IDisposable logger)
        {
            logger.Dispose();
        }
    }
}
