namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A connector command. <see cref="Spec"/> is static so the command tree can be built
/// without constructing the command or starting the host. The host resolves the command
/// from DI and calls <see cref="ExecuteAsync"/>.
/// </summary>
public interface IWorkerCommand
{
    /// <summary>Command name, description, options, and arguments. Read without creating an instance.</summary>
    static abstract CommandSpec Spec { get; }

    /// <summary>
    /// When true, invalid options are reported and <see cref="ExecuteAsync"/> is not called.
    /// Set this to false when the command reports configuration errors itself.
    /// </summary>
    static virtual bool ValidateOptions => true;

    /// <summary>
    /// When true, every invocation records traces and metrics with the same pipeline as <c>run</c>.
    /// Export still happens only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
    /// <c>--telemetry</c> turns this on for a single invocation and does not turn it off.
    /// </summary>
    static virtual bool EnableTelemetry => false;

    Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken);
}
