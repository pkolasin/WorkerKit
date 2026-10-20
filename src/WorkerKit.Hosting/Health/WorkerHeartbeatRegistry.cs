
using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Health;

/// <summary>
/// In-memory <see cref="IWorkerHeartbeat"/>. Timestamps come from <see cref="TimeProvider.GetUtcNow"/>.
/// Registering a name that already exists clears its success, failure count, and last error.
/// </summary>
public sealed class WorkerHeartbeatRegistry : IWorkerHeartbeat
{
    private readonly IOptionsMonitor<HealthOptions> _options;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _workers = new(StringComparer.Ordinal);

    /// <summary>Stores the health options used for grace and the clock used for timestamps.</summary>
    /// <param name="options">Supplies <see cref="HealthOptions.HeartbeatGraceFor"/> when <see cref="Snapshot"/> is taken.</param>
    /// <param name="time">Clock for registration and success times. A failure does not store this clock.</param>
    public WorkerHeartbeatRegistry(IOptionsMonitor<HealthOptions> options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    /// <summary>
    /// Starts tracking <paramref name="workerName"/> at the current clock time.
    /// A second call for the same name resets that worker and replaces its interval.
    /// </summary>
    /// <param name="workerName">Worker name. Compared ordinally.</param>
    /// <param name="interval">Positive interval stored for <see cref="HealthOptions.HeartbeatGraceFor"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="workerName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workerName"/> is empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> is zero or negative.</exception>
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

    /// <summary>
    /// Records a success at the current clock time and clears the consecutive failure count and last error.
    /// </summary>
    /// <param name="workerName">Name passed to <see cref="Register"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="workerName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workerName"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="workerName"/> is not registered.</exception>
    public void Success(string workerName)
    {
        Update(workerName, static (registration, now) => registration.Success(now));
    }

    /// <summary>
    /// Increments the consecutive failure count and stores <paramref name="exception"/>'s message.
    /// An empty message stores the exception type name. The last success time is left unchanged.
    /// </summary>
    /// <param name="workerName">Name passed to <see cref="Register"/>.</param>
    /// <param name="exception">Failure to record. The snapshot keeps the message, not the exception object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> or <paramref name="workerName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workerName"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="workerName"/> is not registered.</exception>
    public void Failure(string workerName, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var error = string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
        Update(workerName, (registration, now) => registration.Failure(now, error));
    }

    /// <summary>
    /// Copies each registration. Grace is <see cref="HealthOptions.HeartbeatGraceFor"/> of that worker's interval.
    /// </summary>
    /// <returns>One snapshot per registered worker. The list is empty when nothing has been registered.</returns>
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
