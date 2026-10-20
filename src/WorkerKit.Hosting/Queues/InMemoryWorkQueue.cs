
using WorkerKit.Abstractions.Queues;
namespace WorkerKit.Hosting.Queues;

/// <summary>
/// EXAMPLE queue kept in the process. It is not durable: a restart drops every item.
/// Production connectors replace <see cref="IWorkQueue{T}"/> with a database table or a broker in a separate project.
/// Claims are in enqueue order. Two items with the same <see cref="WorkItem{T}.PartitionKey"/> are never leased together.
/// </summary>
/// <typeparam name="T">Payload carried by each work item.</typeparam>
/// <remarks>
/// <see cref="TimeProvider.GetUtcNow"/> decides whether a lease has expired, when a released item becomes ready, and which items <see cref="PendingCount"/> counts.
/// </remarks>
public sealed class InMemoryWorkQueue<T> : IWorkQueue<T>, IQueuePendingSource
{
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly List<Entry> _items = [];
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _quarantined = new(StringComparer.Ordinal);
    private readonly List<DeadLetter<T>> _deadLetters = [];

    /// <summary>Creates an empty queue that reads leases and delays from <paramref name="time"/>.</summary>
    /// <param name="time">Clock for lease expiry, release delays, dead-letter times, and <see cref="PendingCount"/>.</param>
    /// <param name="name">Queue name exposed as <see cref="Name"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="time"/> or <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public InMemoryWorkQueue(TimeProvider time, string name)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _time = time;
        Name = name;
    }

    /// <summary>Queue name passed to the constructor. <see cref="IQueuePendingSource"/> uses it as the <c>worker</c> metric tag.</summary>
    public string Name { get; }

    /// <summary>Items waiting to be claimed. Leased, completed, and quarantined items are not included.</summary>
    public long? PendingCount
    {
        get
        {
            lock (_gate)
            {
                var now = _time.GetUtcNow();
                return _items.Count(entry => entry.IsWaiting(now));
            }
        }
    }

    /// <summary>Items removed by <see cref="FailAsync"/>. The returned list is a copy taken under the queue lock.</summary>
    public IReadOnlyList<DeadLetter<T>> DeadLetters
    {
        get
        {
            lock (_gate)
            {
                return _deadLetters.ToArray();
            }
        }
    }

    /// <summary>Appends <paramref name="payload"/> and returns a new id. The item can be claimed immediately.</summary>
    /// <param name="payload">Body stored on the work item.</param>
    /// <param name="partitionKey">
    /// Optional ordering key. Null may run beside any other item.
    /// A non-null key is not leased while another item with that key is leased.
    /// </param>
    /// <returns>The new item id, an <c>n</c>-formatted GUID.</returns>
    public string Enqueue(T payload, string? partitionKey = null)
    {
        var id = Guid.NewGuid().ToString("n");
        lock (_gate)
        {
            _items.Add(new Entry(id, payload, partitionKey));
        }

        return id;
    }

    /// <summary>
    /// Leases up to <paramref name="max"/> ready items for <paramref name="leaseDuration"/>, in enqueue order.
    /// An item is not ready while its lease is still in the future or its available time is still in the future.
    /// An item whose <see cref="WorkItem{T}.PartitionKey"/> is already leased, or already taken in this call, is skipped.
    /// Each returned item has its attempt incremented. The lease end is the current clock time plus <paramref name="leaseDuration"/>.
    /// </summary>
    /// <param name="max">Maximum number of items to lease. Must be at least 1.</param>
    /// <param name="leaseDuration">How long the lease lasts. Must be positive and must not overflow when added to the current time.</param>
    /// <param name="cancellationToken">Checked before the queue is locked. A cancelled token leases nothing.</param>
    /// <returns>The leased items. The list is empty when nothing is ready.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="max"/> is less than 1, <paramref name="leaseDuration"/> is zero or negative,
    /// or adding it to <see cref="TimeProvider.GetUtcNow"/> overflows.
    /// </exception>
    public Task<IReadOnlyList<WorkItem<T>>> ClaimAsync(int max, TimeSpan leaseDuration, CancellationToken cancellationToken)
    {
        if (max < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Claim max must be at least 1.");
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var now = _time.GetUtcNow();
        DateTimeOffset leaseUntil;
        try
        {
            leaseUntil = now + leaseDuration;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), exception.Message);
        }

        lock (_gate)
        {
            var claimed = new List<WorkItem<T>>();
            var blockedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in _items)
            {
                if (entry.IsLeased(now) && entry.PartitionKey is { } leasedKey)
                {
                    blockedKeys.Add(leasedKey);
                }
            }

            foreach (var entry in _items)
            {
                if (claimed.Count >= max)
                {
                    break;
                }

                if (!entry.IsReady(now))
                {
                    continue;
                }

                if (entry.PartitionKey is { } key && !blockedKeys.Add(key))
                {
                    continue;
                }

                entry.Attempt++;
                entry.LeaseUntil = leaseUntil;
                entry.AvailableAt = DateTimeOffset.MinValue;
                claimed.Add(entry.ToItem());
            }

            return Task.FromResult<IReadOnlyList<WorkItem<T>>>(claimed);
        }
    }

    /// <summary>
    /// Removes a leased item. A later call for an id that was already completed returns without changing the queue.
    /// </summary>
    /// <param name="item">Item returned by <see cref="ClaimAsync"/>. Its lease and attempt must still match the queue entry.</param>
    /// <param name="cancellationToken">Checked before the queue is locked.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The id is not in the queue, the entry is quarantined, or the lease and attempt do not match <paramref name="item"/>.
    /// </exception>
    public Task CompleteAsync(WorkItem<T> item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_completed.Contains(item.Id))
            {
                return Task.CompletedTask;
            }

            var entry = Find(item.Id) ?? throw Unknown(item.Id);
            EnsureCurrentLease(entry, item);
            entry.Completed = true;
            entry.LeaseUntil = null;
            _items.Remove(entry);
            _completed.Add(entry.Id);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Clears the lease and makes the item ready after <paramref name="delay"/>.
    /// Null, zero, and negative delays are ready at the current clock time.
    /// An id that was already completed returns without changing the queue.
    /// </summary>
    /// <param name="item">Item returned by <see cref="ClaimAsync"/>. Its lease and attempt must still match the queue entry.</param>
    /// <param name="delay">Wait from <see cref="TimeProvider.GetUtcNow"/> before the item can be claimed again. Null or a non-positive value means no extra wait.</param>
    /// <param name="cancellationToken">Checked before the queue is locked.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The id is not in the queue, the entry is quarantined, or the lease and attempt do not match <paramref name="item"/>.
    /// </exception>
    public Task ReleaseAsync(WorkItem<T> item, TimeSpan? delay, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();
        var wait = delay is { } value && value > TimeSpan.Zero ? value : TimeSpan.Zero;
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_completed.Contains(item.Id))
            {
                return Task.CompletedTask;
            }

            var entry = Find(item.Id) ?? throw Unknown(item.Id);
            EnsureCurrentLease(entry, item);
            entry.LeaseUntil = null;
            entry.AvailableAt = now + wait;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Removes the item and appends a <see cref="DeadLetter{T}"/> stamped with the current clock time.
    /// A later call for an id that was already quarantined returns without changing the queue.
    /// </summary>
    /// <param name="item">Item returned by <see cref="ClaimAsync"/>. Its lease and attempt must still match the queue entry.</param>
    /// <param name="reason">Stored on the dead letter. It cannot be empty.</param>
    /// <param name="cancellationToken">Checked before the queue is locked.</param>
    /// <returns>A completed task.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> or <paramref name="reason"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">
    /// The id is not in the queue, the entry is quarantined, or the lease and attempt do not match <paramref name="item"/>.
    /// </exception>
    public Task FailAsync(WorkItem<T> item, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        cancellationToken.ThrowIfCancellationRequested();
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_quarantined.Contains(item.Id))
            {
                return Task.CompletedTask;
            }

            var entry = Find(item.Id) ?? throw Unknown(item.Id);
            EnsureCurrentLease(entry, item);
            entry.Quarantined = true;
            entry.LeaseUntil = null;
            _items.Remove(entry);
            _quarantined.Add(entry.Id);
            _deadLetters.Add(new DeadLetter<T>(entry.Id, entry.Payload, entry.Attempt, entry.PartitionKey, reason, now));
            return Task.CompletedTask;
        }
    }

    /// <summary>Returns <see cref="PendingCount"/>.</summary>
    /// <param name="cancellationToken">Checked before the count is read.</param>
    /// <returns>The same value as <see cref="PendingCount"/>: items that are not currently leased. Completed and quarantined items are already removed.</returns>
    public Task<long?> GetPendingCountAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PendingCount);
    }

    private Entry? Find(string id)
    {
        foreach (var entry in _items)
        {
            if (entry.Id == id)
            {
                return entry;
            }
        }

        return null;
    }

    private static void EnsureCurrentLease(Entry entry, WorkItem<T> item)
    {
        if (entry.Quarantined)
        {
            throw new InvalidOperationException($"Work item '{item.Id}' is quarantined.");
        }

        if (entry.LeaseUntil != item.LeaseUntil || entry.Attempt != item.Attempt)
        {
            throw new InvalidOperationException($"Work item '{item.Id}' lease is no longer current.");
        }
    }

    private static InvalidOperationException Unknown(string id) =>
        new($"Work item '{id}' is not in the queue.");

    private sealed class Entry(string id, T payload, string? partitionKey)
    {
        public string Id { get; } = id;

        public T Payload { get; } = payload;

        public string? PartitionKey { get; } = partitionKey;

        public int Attempt { get; set; }

        public DateTimeOffset? LeaseUntil { get; set; }

        public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.MinValue;

        public bool Completed { get; set; }

        public bool Quarantined { get; set; }

        public bool IsLeased(DateTimeOffset now) =>
            LeaseUntil is { } until && until > now;

        public bool IsReady(DateTimeOffset now) =>
            !IsLeased(now) && AvailableAt <= now;

        public bool IsWaiting(DateTimeOffset now) =>
            !IsLeased(now);

        public WorkItem<T> ToItem() =>
            new(Id, Payload, Attempt, LeaseUntil ?? DateTimeOffset.MinValue, PartitionKey);
    }
}
