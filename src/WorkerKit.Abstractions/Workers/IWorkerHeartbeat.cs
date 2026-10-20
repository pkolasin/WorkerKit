namespace WorkerKit.Abstractions.Workers;

/// <summary>
/// Per-worker record of the last successful iteration. Periodic workers update it;
/// the heartbeat health check reads <see cref="Snapshot"/>.
/// </summary>
public interface IWorkerHeartbeat
{
    /// <summary>Records <paramref name="workerName"/> and the interval used for its heartbeat.</summary>
    /// <param name="workerName">Worker name.</param>
    /// <param name="interval">Expected time between iterations.</param>
    void Register(string workerName, TimeSpan interval);

    /// <summary>Records a successful iteration for <paramref name="workerName"/>.</summary>
    /// <param name="workerName">Worker that succeeded.</param>
    void Success(string workerName);

    /// <summary>Records a failed iteration for <paramref name="workerName"/>.</summary>
    /// <param name="workerName">Worker that failed.</param>
    /// <param name="exception">Failure recorded for the worker.</param>
    void Failure(string workerName, Exception exception);

    /// <summary>Returns the current record for each registered worker.</summary>
    /// <returns>One <see cref="WorkerHeartbeatSnapshot"/> per registered worker.</returns>
    IReadOnlyList<WorkerHeartbeatSnapshot> Snapshot();
}
