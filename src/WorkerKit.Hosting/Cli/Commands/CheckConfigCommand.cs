using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting;

namespace WorkerKit.Hosting.Cli.Commands;

internal static class CheckConfigCommand
{
    public static Command Create(WorkerHostPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var command = new Command("check-config", "Validate options and print a redacted configuration summary.");
        command.TreatUnmatchedTokensAsErrors = false;
        // --telemetry is accepted because it is recursive, and this command ignores it.
        command.SetAction((_, cancellationToken) => CommandHost.InvokeAsync(
            plan.Args,
            (services, _) =>
            {
                var handler = services.GetRequiredService<CheckConfigHandler>();
                return Task.FromResult(handler.Execute());
            },
            cancellationToken,
            validate: false,
            enableTelemetry: false,
            identity: plan.Identity,
            configureHost: plan.HasConfiguration ? plan.Apply : null,
            operatingSystemIntegration: plan.OperatingSystemIntegration));
        return command;
    }
}
