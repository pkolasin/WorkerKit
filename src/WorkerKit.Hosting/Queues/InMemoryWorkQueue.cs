
using WorkerKit.Abstractions.Queues;
namespace WorkerKit.Hosting.Queues;

/// <summary>
/// EXAMPLE queue kept in the process. It is not durable: a restart drops every item.
/// Production connectors replace <see cref="IWorkQueue{T}"/> with a database table or a broker in a separate project.
/// Claims are in enqueue order. Two items with the same <see cref="WorkItem{T}.PartitionKey"/> are never leased together.
/// </summary>
public sealed class InMemoryWorkQueue<T> : IWorkQueue<T>, IQueuePendingSource
{
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly List<Entry> _items = [];
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _quarantined = new(StringComparer.Ordinal);
    private readonly List<DeadLetter<T>> _deadLetters = [];

    public InMemoryWorkQueue(TimeProvider time, string name)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _time = time;
        Name = name;
    }

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

    public string Enqueue(T payload, string? partitionKey = null)
    {
        var id = Guid.NewGuid().ToString("n");
        lock (_gate)
        {
            _items.Add(new Entry(id, payload, partitionKey));
        }

        return id;
    }

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
