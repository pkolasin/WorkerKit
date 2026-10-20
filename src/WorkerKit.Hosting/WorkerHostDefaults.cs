namespace WorkerKit.Hosting;

/// <summary>Application and service names from <see cref="AppIdentity.Current"/>.</summary>
public static class WorkerHostDefaults
{
    /// <summary>Product name from <see cref="AppIdentity.Current"/>.</summary>
    public static string ApplicationName => AppIdentity.Current.Name;

    /// <summary>Windows service name for the default instance, from <see cref="AppIdentity.Current"/>.</summary>
    public static string ServiceName => AppIdentity.Current.ServiceName;

    /// <summary>
    /// Windows service name for an instance. <c>default</c> stays <see cref="ServiceName"/> so an existing
    /// single-service install is unchanged. Named instances are <c>{service}-&lt;name&gt;</c>.
    /// </summary>
    /// <param name="instanceName">Instance name passed to <see cref="WorkerKit.Abstractions.Identity.WorkerAppIdentity.ServiceNameFor(string)"/>.</param>
    /// <returns>The service name for <paramref name="instanceName"/>.</returns>
    public static string ServiceNameFor(string instanceName) => AppIdentity.Current.ServiceNameFor(instanceName);
}
