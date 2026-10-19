namespace WorkerKit.Abstractions.Workers;

/// <summary>
/// Per-worker record of the last successful iteration. Periodic workers update it;
/// the heartbeat health check reads <see cref="Snapshot"/>.
/// </summary>
public interface IWorkerHeartbeat
{
    void Register(string workerName, TimeSpan interval);

    void Success(string workerName);

    void Failure(string workerName, Exception exception);

    IReadOnlyList<WorkerHeartbeatSnapshot> Snapshot();
}
