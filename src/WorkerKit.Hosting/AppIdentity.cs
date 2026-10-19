using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting;

/// <summary>
/// Identity for this process. Help and instance resolution use it before the generic host exists.
/// </summary>
public static class AppIdentity
{
    public static WorkerAppIdentity Current { get; } =
        WorkerAppIdentity.FromEntryAssembly(typeof(AppIdentity).Assembly);
}
