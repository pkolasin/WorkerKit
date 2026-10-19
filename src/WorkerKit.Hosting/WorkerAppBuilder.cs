using WorkerKit.Abstractions.Identity;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting;

/// <summary>
/// Deferred application builder. <see cref="Commands"/> and <see cref="ConfigureHost"/> only collect work.
/// <see cref="RunAsync"/> builds the command tree, parses the original arguments, and creates a host
/// for the command that was selected. <c>help</c> and <c>version</c> do not create one.
/// </summary>
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

    public WorkerAppIdentity Identity { get; }

    /// <summary>Command catalog from the extensible command tree. Built-in commands are already registered.</summary>
    public WorkerCommands Commands => _commands;

    /// <summary>
    /// Adds a host callback. Callbacks run in registration order, after instance, service integration,
    /// Serilog, and health, and before telemetry. The builder passed in exposes <see cref="IInstanceContext"/>
    /// through <see cref="WorkerApplicationBuilderExtensions.GetInstance"/>.
    /// </summary>
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
    public WorkerAppBuilder UseOperatingSystemIntegration(bool enabled)
    {
        _commands.UseOperatingSystemIntegration(enabled);
        return this;
    }

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
