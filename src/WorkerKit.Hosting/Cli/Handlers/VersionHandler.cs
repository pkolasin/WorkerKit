using System.Reflection;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting;

namespace WorkerKit.Hosting.Cli.Handlers;

/// <summary>Prints the application assembly name and version.</summary>
public sealed class VersionHandler
{
    private readonly ICommandOutput _output;

    /// <summary>Creates a handler that writes to <paramref name="output"/>.</summary>
    /// <param name="output">Destination for the version line.</param>
    public VersionHandler(ICommandOutput output)
    {
        _output = output;
    }

    /// <summary>Writes <see cref="Describe()"/> and returns <see cref="ExitCodes.Success"/>.</summary>
    /// <returns><see cref="ExitCodes.Success"/>.</returns>
    public int Execute()
    {
        _output.WriteLine(Describe());
        return ExitCodes.Success;
    }

    /// <summary>Formats the version of <see cref="AppIdentity.Current"/>.</summary>
    /// <returns>The assembly name and version, separated by a space.</returns>
    public static string Describe() => Describe(AppIdentity.Current);

    /// <summary>
    /// Formats <paramref name="identity"/> as the application assembly's simple name and version.
    /// The version is the informational version when it is not null, otherwise the assembly version, otherwise <c>unknown</c>.
    /// </summary>
    /// <param name="identity">Identity whose <see cref="WorkerAppIdentity.ApplicationAssembly"/> supplies the name and version.</param>
    /// <returns>The assembly name and version, separated by a space.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
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
