using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting;

/// <summary>
/// Identity for this process. Help and instance resolution use it before the generic host exists.
/// </summary>
public static class AppIdentity
{
    /// <summary>
    /// Identity from <see cref="WorkerAppIdentity.FromEntryAssembly"/>, using this assembly when the entry assembly is a test host.
    /// </summary>
    public static WorkerAppIdentity Current { get; } =
        WorkerAppIdentity.FromEntryAssembly(typeof(AppIdentity).Assembly);
}
