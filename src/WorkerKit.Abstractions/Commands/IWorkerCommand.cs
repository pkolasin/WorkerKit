namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A connector command. <see cref="Spec"/> is static so the command tree can be built
/// without constructing the command or starting the host. The host resolves the command
/// from DI and calls <see cref="ExecuteAsync"/>.
/// </summary>
/// <example>
/// <code>
/// <![CDATA[
/// public sealed class LookupCommand(IMyClient client) : IWorkerCommand
/// {
///     static readonly CommandOption<string> Id = new("--id", "Record id.") { Required = true };
///
///     public static CommandSpec Spec { get; } = new("lookup", "Look up one record.") { Id };
///
///     public async Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
///         => await client.GetAsync(args.Get(Id), cancellationToken) is null ? 1 : 0;
/// }
/// ]]>
/// </code>
/// </example>
public interface IWorkerCommand
{
    /// <summary>Command name, description, options, and arguments. Read without creating an instance.</summary>
    static abstract CommandSpec Spec { get; }

    /// <summary>
    /// When true, invalid options are reported and <see cref="ExecuteAsync"/> is not called.
    /// Set this to false when the command reports configuration errors itself.
    /// </summary>
    /// <remarks>The default implementation returns <see langword="true"/>.</remarks>
    static virtual bool ValidateOptions => true;

    /// <summary>
    /// When true, every invocation records traces and metrics with the same pipeline as <c>run</c>.
    /// Export still happens only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set.
    /// <c>--telemetry</c> turns this on for a single invocation and does not turn it off.
    /// </summary>
    /// <remarks>The default implementation returns <see langword="false"/>.</remarks>
    static virtual bool EnableTelemetry => false;

    /// <summary>Runs the command for one invocation.</summary>
    /// <param name="args">Parsed options and arguments.</param>
    /// <param name="cancellationToken">Token the caller can use to cancel the invocation.</param>
    /// <returns>A task whose result is the process exit code.</returns>
    Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken);
}
