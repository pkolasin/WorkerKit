using System.Diagnostics.CodeAnalysis;
using WorkerKit.Abstractions.Commands;
using WorkerKit.Hosting.Cli;

namespace WorkerKit.Hosting;

/// <summary>
/// Attaches the host action to an <see cref="IWorkerCommand"/>. The command tree is built from
/// <c>TCommand.Spec</c> and does not construct the command. Execution resolves it from DI.
/// </summary>
internal static class WorkerCommandBinding
{
    public static Command Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(
        string[] args,
        Option<bool> telemetry,
        WorkerHostPlan plan)
        where TCommand : class, IWorkerCommand
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentNullException.ThrowIfNull(plan);
        var spec = TCommand.Spec ?? throw new InvalidOperationException("The command spec is missing.");
        var defined = CommandSpecTranslator.Translate(spec, out var symbols);
        plan.Add(static builder => builder.Services.AddScoped<TCommand>());
        defined.SetAction((parseResult, cancellationToken) => CommandHost.InvokeAsync(
            args,
            (services, token) =>
            {
                var command = services.GetRequiredService<TCommand>();
                return command.ExecuteAsync(new ParsedCommandArgs(parseResult, symbols), token);
            },
            cancellationToken,
            TCommand.ValidateOptions,
            TCommand.EnableTelemetry || parseResult.GetValue(telemetry),
            plan.Identity,
            plan.HasConfiguration ? plan.Apply : null,
            plan.OperatingSystemIntegration));
        return defined;
    }
}
