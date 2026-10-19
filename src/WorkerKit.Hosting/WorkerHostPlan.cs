using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting;

/// <summary>
/// Collected host delegates and identity. Creating the plan does not load configuration or build a host.
/// The chosen command applies the delegates when it creates the host.
/// </summary>
internal sealed class WorkerHostPlan
{
    private readonly List<Action<IHostApplicationBuilder>> _configure = [];

    public WorkerHostPlan(string[] args, WorkerAppIdentity? identity)
    {
        Args = args;
        Identity = identity;
    }

    public string[] Args { get; }

    public WorkerAppIdentity? Identity { get; }

    /// <summary>When false, command hosts skip Windows Service and systemd registration. Production leaves this true.</summary>
    public bool OperatingSystemIntegration { get; set; } = true;

    public bool HasConfiguration => _configure.Count > 0;

    public void Add(Action<IHostApplicationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configure.Add(configure);
    }

    public void Apply(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var configure in _configure)
        {
            configure(builder);
        }
    }
}
