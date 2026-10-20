using System.Diagnostics.CodeAnalysis;
using WorkerKit.Abstractions.Commands;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli.Commands;

namespace WorkerKit.Hosting;

/// <summary>
/// Command catalog. Built-in commands are <c>run</c> (also the default with no command), <c>check-config</c>,
/// <c>version</c>, and <c>help</c>, plus recursive <c>--instance</c>, <c>--instance-dir</c>, and <c>--telemetry</c>.
/// Connector commands are added with <see cref="Add{TCommand}"/> and <see cref="AddGroup"/>.
/// <see cref="WorkerAppBuilder"/> exposes this object as <c>Commands</c>.
/// </summary>
public sealed class WorkerCommands
{
    internal static readonly string[] BuiltInNames = ["run", "check-config", "version", "help"];

    private readonly string[] _args;
    private readonly WorkerHostPlan _plan;
    private readonly List<Command> _added = [];
    private readonly HashSet<string> _rootNames = new(BuiltInNames, StringComparer.Ordinal);
    private readonly Option<bool> _telemetry = new("--telemetry")
    {
        Description = "Record traces and metrics for this connector command. Ignored by run, check-config, help, and version.",
        Recursive = true,
    };
    private RootCommand? _root;

    private WorkerCommands(string[] args, WorkerAppIdentity? identity)
    {
        _args = args;
        _plan = new WorkerHostPlan(args, identity);
    }

    /// <summary>Creates an empty catalog. Built-in commands are added when the tree is built.</summary>
    /// <param name="args">Arguments stored for the host when a command builds one.</param>
    /// <param name="identity">Application identity. When null, commands that need an identity use <see cref="AppIdentity.Current"/>.</param>
    /// <returns>The catalog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    public static WorkerCommands Create(string[] args, WorkerAppIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new WorkerCommands(args, identity);
    }

    /// <summary>
    /// Records a host callback. It runs only when a command builds the host, after framework services.
    /// <see cref="WorkerAppBuilder.ConfigureHost"/> is the public entry point.
    /// </summary>
    internal WorkerCommands ConfigureHost(Action<IHostApplicationBuilder> configure)
    {
        EnsureOpen();
        _plan.Add(configure);
        return this;
    }

    internal void ApplyHost(IHostApplicationBuilder builder) => _plan.Apply(builder);

    internal void UseOperatingSystemIntegration(bool enabled) => _plan.OperatingSystemIntegration = enabled;

    /// <summary>Adds <typeparamref name="TCommand"/> at the root.</summary>
    /// <typeparam name="TCommand">Command type. Its <see cref="IWorkerCommand.Spec"/> supplies the name and symbols.</typeparam>
    /// <returns>This catalog.</returns>
    /// <remarks>
    /// The command name and its aliases cannot be <c>run</c>, <c>check-config</c>, <c>version</c>, or <c>help</c>.
    /// Telemetry for the command is on when <see cref="IWorkerCommand.EnableTelemetry"/> is true or the caller passes <c>--telemetry</c>.
    /// </remarks>
    public WorkerCommands Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>()
        where TCommand : class, IWorkerCommand
    {
        EnsureOpen();
        var command = WorkerCommandBinding.Bind<TCommand>(_args, _telemetry, _plan);
        WorkerCommandNames.Reserve(_rootNames, command, builtIn: true);
        _added.Add(command);
        return this;
    }

    /// <summary>Adds a root group and runs <paramref name="configure"/> before the group is attached.</summary>
    /// <param name="name">Group name.</param>
    /// <param name="description">Group description.</param>
    /// <param name="configure">Callback that adds commands to the new group.</param>
    /// <returns>This catalog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <remarks>The group name cannot be <c>run</c>, <c>check-config</c>, <c>version</c>, or <c>help</c>.</remarks>
    public WorkerCommands AddGroup(string name, string description, Action<WorkerCommandGroup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureOpen();
        var group = WorkerCommandNames.CreateGroup(name, description);
        WorkerCommandNames.Reserve(_rootNames, group, builtIn: true);
        configure(new WorkerCommandGroup(group, _args, _telemetry, _plan));
        _added.Add(group);
        return this;
    }

    /// <summary>
    /// Builds the root command, including <c>run</c>, <c>check-config</c>, <c>version</c>, <c>help</c>, and the commands added so far.
    /// A second call returns the same command.
    /// </summary>
    /// <returns>The root command. With no command, it runs the host.</returns>
    /// <remarks>
    /// <c>--instance-dir</c>, <c>--instance</c>, and <c>--telemetry</c> are recursive on the root.
    /// <c>run</c> records telemetry on every invocation. <c>check-config</c>, <c>help</c>, and <c>version</c> ignore <c>--telemetry</c>.
    /// </remarks>
    public RootCommand Build()
    {
        if (_root is not null)
        {
            return _root;
        }

        var identity = _plan.Identity ?? AppIdentity.Current;
        var root = new RootCommand("Worker host for data connectors. With no command, starts the host.");
        // Recursive so every command accepts the options and help lists them. The original args are still
        // forwarded to the generic host, which turns them into the harmless keys "instance-dir" and "instance".
        root.Add(new Option<string>("--instance-dir")
        {
            Description = "Instance directory (configuration, logs, and data). Overrides " + identity.InstanceDirectoryVariable + ".",
            Recursive = true,
        });
        root.Add(new Option<string>("--instance")
        {
            Description = "Instance name. With no --instance-dir, the directory is " + identity.InstancesRootVariable + "/<name>.",
            Recursive = true,
        });
        root.Add(_telemetry);
        root.Add(RunCommand.Create(_plan));
        root.Add(CheckConfigCommand.Create(_plan));
        root.Add(VersionCommand.Create(_plan.Identity));
        foreach (var command in _added)
        {
            root.Add(command);
        }

        root.Add(HelpCommand.Create(() => root, identity));
        root.SetAction((_, cancellationToken) => RunCommand.ExecuteAsync(_plan, cancellationToken));
        _root = root;
        return root;
    }

    /// <summary>Builds the tree and parses <paramref name="args"/>.</summary>
    /// <param name="args">Arguments to parse.</param>
    /// <returns>The parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is null.</exception>
    public ParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Build().Parse(args);
    }

    private void EnsureOpen()
    {
        if (_root is not null)
        {
            throw new InvalidOperationException("Commands cannot be added after the command tree is built.");
        }
    }
}
