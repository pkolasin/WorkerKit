namespace WorkerKit.Abstractions.Workers;

/// <summary>
/// Point-in-time view of one registered worker. <see cref="Grace"/> is computed from the interval
/// and the current <c>Health</c> options when the snapshot is taken.
/// </summary>
/// <param name="Name">Name of the registered worker.</param>
/// <param name="Registered">Instant the worker was registered.</param>
/// <param name="LastSuccess">Instant of the last recorded success, or <see langword="null"/> when none has been recorded.</param>
/// <param name="Interval">Interval the worker was registered with.</param>
/// <param name="Grace">Grace period stored in this snapshot.</param>
/// <param name="ConsecutiveFailures">Consecutive failures recorded for the worker.</param>
/// <param name="LastError">Last recorded error text, or <see langword="null"/> when none is recorded.</param>
public sealed record WorkerHeartbeatSnapshot(
    string Name,
    DateTimeOffset Registered,
    DateTimeOffset? LastSuccess,
    TimeSpan Interval,
    TimeSpan Grace,
    int ConsecutiveFailures,
    string? LastError);
