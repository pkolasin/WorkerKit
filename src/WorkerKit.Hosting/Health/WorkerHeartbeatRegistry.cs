
using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Health;

public sealed class WorkerHeartbeatRegistry : IWorkerHeartbeat
{
    private readonly IOptionsMonitor<HealthOptions> _options;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _workers = new(StringComparer.Ordinal);

    public WorkerHeartbeatRegistry(IOptionsMonitor<HealthOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public void Register(string workerName, TimeSpan interval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Worker interval must be positive.");
        }

        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_workers.TryGetValue(workerName, out var existing))
            {
                existing.Reset(now, interval);
                return;
            }

            _workers.Add(workerName, new Registration(workerName, now, interval));
        }
    }

    public void Success(string workerName)
    {
        Update(workerName, static (registration, now) => registration.Success(now));
    }

    public void Failure(string workerName, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var error = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
        Update(workerName, (registration, now) => registration.Failure(now, error));
    }

    public IReadOnlyList<WorkerHeartbeatSnapshot> Snapshot()
    {
        var options = _options.CurrentValue;
        lock (_gate)
        {
            var snapshots = new WorkerHeartbeatSnapshot[_workers.Count];
            var index = 0;
            foreach (var registration in _workers.Values)
            {
                snapshots[index++] = registration.ToSnapshot(options.HeartbeatGraceFor(registration.Interval));
            }

            return snapshots;
        }
    }

    private void Update(string workerName, Action<Registration, DateTimeOffset> change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (!_workers.TryGetValue(workerName, out var registration))
            {
                throw new InvalidOperationException($"Worker '{workerName}' is not registered.");
            }

            change(registration, now);
        }
    }

    private sealed class Registration
    {
        public Registration(string name, DateTimeOffset registered, TimeSpan interval)
        {
            Name = name;
            Registered = registered;
            Interval = interval;
        }

        public string Name { get; }

        public DateTimeOffset Registered { get; private set; }

        public TimeSpan Interval { get; private set; }

        public DateTimeOffset? LastSuccess { get; private set; }

        public int ConsecutiveFailures { get; private set; }

        public string? LastError { get; private set; }

        public void Reset(DateTimeOffset registered, TimeSpan interval)
        {
            Registered = registered;
            Interval = interval;
            LastSuccess = null;
            ConsecutiveFailures = 0;
            LastError = null;
        }

        public void Success(DateTimeOffset now)
        {
            LastSuccess = now;
            ConsecutiveFailures = 0;
            LastError = null;
        }

        public void Failure(DateTimeOffset now, string error)
        {
            _ = now;
            ConsecutiveFailures++;
            LastError = error;
        }

        public WorkerHeartbeatSnapshot ToSnapshot(TimeSpan grace)
        {
            return new WorkerHeartbeatSnapshot(
                Name,
                Registered,
                LastSuccess,
                Interval,
                grace,
                ConsecutiveFailures,
                LastError);
        }
    }
}
