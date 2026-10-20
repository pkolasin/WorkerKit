using WorkerKit.Abstractions.Identity;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting;

/// <summary>
/// Deferred application builder. <see cref="Commands"/> and <see cref="ConfigureHost"/> only collect work.
/// <see cref="RunAsync"/> builds the command tree, parses the original arguments, and creates a host
/// for the command that was selected. <c>help</c> and <c>version</c> do not create one.
/// </summary>
/// <example>
/// <code>
/// app.Commands.Add&lt;LookupCommand&gt;();
/// app.ConfigureHost(builder => builder.Services.AddHostedService&lt;MyWorker&gt;());
/// return await app.RunAsync();
/// </code>
/// </example>
public sealed class WorkerAppBuilder
{
    private readonly string[] _args;
    private readonly WorkerCommands _commands;
    private bool _started;

    internal WorkerAppBuilder(string[] args, WorkerAppIdentity identity)
    {
        _args = args;
        Identity = identity;
        _commands = WorkerCommands.Create(args, identity);
    }

    /// <summary>Application identity captured when this builder was created.</summary>
    public WorkerAppIdentity Identity { get; }

    /// <summary>Command catalog from the extensible command tree. Built-in commands are already registered.</summary>
    public WorkerCommands Commands => _commands;

    /// <summary>
    /// Adds a host callback. Callbacks run in registration order, after instance, service integration,
    /// Serilog, and health, and before telemetry. The builder passed in exposes <see cref="IInstanceContext"/>
    /// through <see cref="WorkerApplicationBuilderExtensions.GetInstance"/>.
    /// </summary>
    /// <param name="configure">Callback invoked when a command builds the host.</param>
    /// <returns>This builder.</returns>
    /// <remarks>Throws <see cref="InvalidOperationException"/> when this builder has already been run.</remarks>
    public WorkerAppBuilder ConfigureHost(Action<IHostApplicationBuilder> configure)
    {
        EnsureOpen();
        _commands.ConfigureHost(configure);
        return this;
    }

    /// <summary>
    /// Builds a host from the <see cref="ConfigureHost"/> delegates collected so far.
    /// This does not parse a command, so <c>help</c> and <c>version</c> still skip the host.
    /// <paramref name="operatingSystemIntegration"/> defaults to true. Tests pass false.
    /// </summary>
    /// <param name="contentRoot">Content root passed to <see cref="WorkerHostBuilder.Create"/>. Null leaves that choice to <see cref="WorkerHostBuilder"/>.</param>
    /// <param name="environmentName">Host environment name passed to <see cref="WorkerHostBuilder.Create"/>.</param>
    /// <param name="configure">Callback invoked after the collected <see cref="ConfigureHost"/> delegates.</param>
    /// <param name="enableTelemetry">When true, the host records traces and metrics. The parameter default is false.</param>
    /// <param name="operatingSystemIntegration">When true, the host registers Windows Service and systemd integration. The parameter default is true.</param>
    /// <returns>The host builder. The host is not built or started.</returns>
    /// <remarks>
    /// Telemetry is registered only when <paramref name="enableTelemetry"/> is true.
    /// Throws <see cref="InvalidOperationException"/> when this builder has already been run.
    /// </remarks>
    public HostApplicationBuilder BuildHost(
        string? contentRoot = null,
        string? environmentName = null,
        Action<HostApplicationBuilder>? configure = null,
        bool enableTelemetry = false,
        bool operatingSystemIntegration = true)
    {
        EnsureOpen();
        return WorkerHostBuilder.Create(
            _args,
            contentRoot,
            environmentName,
            configure,
            enableTelemetry,
            Identity,
            _commands.ApplyHost,
            operatingSystemIntegration);
    }

    /// <summary>
    /// When false, every host built from this recipe skips Windows Service and systemd registration.
    /// The default is true. <c>WorkerKit.Testing</c> passes false for the iteration host and for <c>RunCommandAsync</c>.
    /// </summary>
    /// <param name="enabled">False skips Windows Service and systemd registration on hosts built from this recipe.</param>
    /// <returns>This builder.</returns>
    public WorkerAppBuilder UseOperatingSystemIntegration(bool enabled)
    {
        _commands.UseOperatingSystemIntegration(enabled);
        return this;
    }

    /// <summary>Parses the original arguments and invokes the selected command.</summary>
    /// <param name="cancellationToken">Token passed to command invocation. The parameter default is <see langword="default"/>.</param>
    /// <returns>The command's exit code.</returns>
    /// <exception cref="InvalidOperationException">The application has already been run.</exception>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            throw new InvalidOperationException("The application can only be run once.");
        }

        _started = true;
        return await _commands.Parse(_args).InvokeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private void EnsureOpen()
    {
        if (_started)
        {
            throw new InvalidOperationException("The host recipe cannot change after the application has started.");
        }
    }
}
