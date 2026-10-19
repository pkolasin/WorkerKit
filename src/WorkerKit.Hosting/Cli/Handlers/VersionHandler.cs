using System.Reflection;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting;

namespace WorkerKit.Hosting.Cli.Handlers;

public sealed class VersionHandler
{
    private readonly ICommandOutput _output;

    public VersionHandler(ICommandOutput output)
    {
        _output = output;
    }

    public int Execute()
    {
        _output.WriteLine(Describe());
        return ExitCodes.Success;
    }

    public static string Describe() => Describe(AppIdentity.Current);

    public static string Describe(WorkerAppIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var assembly = identity.ApplicationAssembly;
        var name = assembly.GetName();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational ?? name.Version?.ToString() ?? "unknown";
        return $"{name.Name} {version}";
    }
}
