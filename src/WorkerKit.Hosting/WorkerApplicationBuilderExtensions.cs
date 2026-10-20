
using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting;

/// <summary>Reads the instance stored on a worker host builder.</summary>
public static class WorkerApplicationBuilderExtensions
{
    /// <summary>
    /// Instance resolved for this host. Available inside <c>ConfigureHost</c> and from
    /// <see cref="WorkerHostBuilder"/>, including a host built in a test.
    /// </summary>
    /// <param name="builder">Host builder that should already carry an instance.</param>
    /// <returns>The stored <see cref="IInstanceContext"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The builder has no stored instance.</exception>
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
