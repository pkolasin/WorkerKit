using System.Diagnostics.CodeAnalysis;
using WorkerKit.Abstractions.Commands;

namespace WorkerKit.Hosting;

/// <summary>
/// A subcommand group. Children are connector commands or nested groups.
/// A grouped command may reuse a built-in name; the full path is what the parser sees.
/// </summary>
public sealed class WorkerCommandGroup
{
    private readonly Command _group;
    private readonly string[] _args;
    private readonly Option<bool> _telemetry;
    private readonly WorkerHostPlan _plan;
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    internal WorkerCommandGroup(Command group, string[] args, Option<bool> telemetry, WorkerHostPlan plan)
    {
        _group = group;
        _args = args;
        _telemetry = telemetry;
        _plan = plan;
    }

    public WorkerCommandGroup Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>()
        where TCommand : class, IWorkerCommand
    {
        var command = WorkerCommandBinding.Bind<TCommand>(_args, _telemetry, _plan);
        WorkerCommandNames.Reserve(_names, command, builtIn: false);
        _group.Add(command);
        return this;
    }

    public WorkerCommandGroup AddGroup(string name, string description, Action<WorkerCommandGroup> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var nested = WorkerCommandNames.CreateGroup(name, description);
        WorkerCommandNames.Reserve(_names, nested, builtIn: false);
        configure(new WorkerCommandGroup(nested, _args, _telemetry, _plan));
        _group.Add(nested);
        return this;
    }
}
