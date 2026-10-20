using WorkerKit.Hosting.Queues;

namespace WorkerKit.Testing;

/// <summary>Reads an <see cref="InMemoryWorkQueue{T}"/> resolved from the test host.</summary>
public static class InMemoryQueue
{
    /// <summary>Resolves the in-memory queue registered for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Work-item payload type.</typeparam>
    /// <param name="services">Provider that contains <see cref="InMemoryWorkQueue{T}"/>.</param>
    /// <returns>The queue from <paramref name="services"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static InMemoryWorkQueue<T> Of<T>(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<InMemoryWorkQueue<T>>();
    }

    /// <summary>
    /// Items waiting on the queue for <typeparamref name="T"/>.
    /// A <see langword="null"/> <see cref="InMemoryWorkQueue{T}.PendingCount"/> becomes 0.
    /// </summary>
    /// <typeparam name="T">Work-item payload type.</typeparam>
    /// <param name="services">Provider passed to <see cref="Of{T}"/>.</param>
    /// <returns><see cref="InMemoryWorkQueue{T}.PendingCount"/>, or 0 when that value is <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static long Pending<T>(IServiceProvider services)
    {
        var pending = Of<T>(services).PendingCount;
        return pending ?? 0;
    }

    /// <summary>Dead letters stored on the queue for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Work-item payload type.</typeparam>
    /// <param name="services">Provider passed to <see cref="Of{T}"/>.</param>
    /// <returns><see cref="InMemoryWorkQueue{T}.DeadLetters"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<DeadLetter<T>> DeadLetters<T>(IServiceProvider services) =>
        Of<T>(services).DeadLetters;
}
