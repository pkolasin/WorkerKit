using System.Reflection;
using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting;

/// <summary>
/// Identity overrides for <see cref="WorkerApp.CreateBuilder{TProgram}"/>.
/// Setting these does not load configuration or create an instance directory.
/// </summary>
public sealed class WorkerAppOptions
{
    /// <summary>Product name. The default is the entry assembly product, or its name without a trailing <c>.Host</c>.</summary>
    public string? Name { get; set; }

    /// <summary>Windows service name for the default instance. The default is <see cref="Name"/>.</summary>
    public string? ServiceName { get; set; }

    /// <summary>Environment variable prefix. The default is derived from <see cref="Name"/>.</summary>
    public string? EnvPrefix { get; set; }

    /// <summary>
    /// Assembly that owns user secrets and <c>service.version</c>.
    /// The default is <c>TProgram</c>'s assembly. A test host falls back to <see cref="Assembly.GetEntryAssembly"/>.
    /// </summary>
    public Assembly? ApplicationAssembly { get; set; }
}

public static class WorkerApp
{
    /// <summary>
    /// Starts a recipe. The returned builder records commands and <c>ConfigureHost</c> delegates only.
    /// <see cref="WorkerAppBuilder.RunAsync"/> parses <paramref name="args"/> and builds a host for the chosen command.
    /// </summary>
    public static WorkerAppBuilder CreateBuilder<TProgram>(string[] args, Action<WorkerAppOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new WorkerAppOptions();
        configure?.Invoke(options);
        var assembly = ResolveAssembly<TProgram>(options);
        var identity = WorkerAppIdentity.Create(assembly, options.Name, options.ServiceName, options.EnvPrefix);
        return new WorkerAppBuilder(args, identity);
    }

    private static Assembly ResolveAssembly<TProgram>(WorkerAppOptions options)
    {
        if (options.ApplicationAssembly is not null)
        {
            return options.ApplicationAssembly;
        }

        var program = typeof(TProgram).Assembly;
        if (!IsTestHost(program))
        {
            return program;
        }

        var entry = Assembly.GetEntryAssembly();
        if (entry is not null && !IsTestHost(entry))
        {
            return entry;
        }

        return program;
    }

    private static bool IsTestHost(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        return string.IsNullOrEmpty(name)
            || name.Equals("testhost", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("testhost.", StringComparison.OrdinalIgnoreCase);
    }
}
