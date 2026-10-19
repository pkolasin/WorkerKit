using WorkerKit.Hosting.Queues;

namespace WorkerKit.Testing;

/// <summary>Reads an <see cref="InMemoryWorkQueue{T}"/> resolved from the test host.</summary>
public static class InMemoryQueue
{
    public static InMemoryWorkQueue<T> Of<T>(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<InMemoryWorkQueue<T>>();
    }

    public static long Pending<T>(IServiceProvider services)
    {
        var pending = Of<T>(services).PendingCount;
        return pending ?? 0;
    }

    public static IReadOnlyList<DeadLetter<T>> DeadLetters<T>(IServiceProvider services) =>
        Of<T>(services).DeadLetters;
}
