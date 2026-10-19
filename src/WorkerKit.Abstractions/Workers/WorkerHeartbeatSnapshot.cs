namespace WorkerKit.Abstractions.Workers;

/// <summary>
/// Point-in-time view of one registered worker. <see cref="Grace"/> is computed from the interval
/// and the current <c>Health</c> options when the snapshot is taken.
/// </summary>
public sealed record WorkerHeartbeatSnapshot(
    string Name,
    DateTimeOffset Registered,
    DateTimeOffset? LastSuccess,
    TimeSpan Interval,
    TimeSpan Grace,
    int ConsecutiveFailures,
    string? LastError);
