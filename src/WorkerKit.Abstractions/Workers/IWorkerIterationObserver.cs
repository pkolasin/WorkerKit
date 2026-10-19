namespace WorkerKit.Abstractions.Workers;

/// <summary>
/// Hook invoked around each <c>PeriodicWorker</c> iteration.
/// <c>run</c> and a connector command that enables telemetry register the OpenTelemetry implementation. Other commands keep the no-op.
/// </summary>
public interface IWorkerIterationObserver
{
    void IterationStarting(string workerName);

    void IterationSucceeded(string workerName, TimeSpan duration, int itemCount);

    void IterationFailed(string workerName, TimeSpan duration, Exception exception);

    /// <summary>
    /// The iteration was cancelled. Drop the in-flight span and do not record a failure.
    /// </summary>
    void IterationAbandoned(string workerName);
}
