namespace WorkerKit.Hosting;

public static class WorkerHostDefaults
{
    public static string ApplicationName => AppIdentity.Current.Name;

    public static string ServiceName => AppIdentity.Current.ServiceName;

    /// <summary>
    /// Windows service name for an instance. <c>default</c> stays <see cref="ServiceName"/> so an existing
    /// single-service install is unchanged. Named instances are <c>{service}-&lt;name&gt;</c>.
    /// </summary>
    public static string ServiceNameFor(string instanceName) => AppIdentity.Current.ServiceNameFor(instanceName);
}
