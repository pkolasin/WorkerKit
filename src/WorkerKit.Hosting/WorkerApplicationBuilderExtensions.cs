
using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting;

public static class WorkerApplicationBuilderExtensions
{
    /// <summary>
    /// Instance resolved for this host. Available inside <c>ConfigureHost</c> and from
    /// <see cref="WorkerHostBuilder"/>, including a host built in a test.
    /// </summary>
    public static IInstanceContext GetInstance(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Properties.TryGetValue(typeof(IInstanceContext), out var stored)
            && stored is IInstanceContext instance)
        {
            return instance;
        }

        throw new InvalidOperationException(
            "This host builder has no instance. Call GetInstance from ConfigureHost or from a WorkerHostBuilder callback.");
    }

    internal static void RememberInstance(this IHostApplicationBuilder builder, IInstanceContext instance)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(instance);
        builder.Properties[typeof(IInstanceContext)] = instance;
    }
}
