namespace WorkerKit.Abstractions.Workers;

/// <summary>
/// Hook invoked around each <c>PeriodicWorker</c> iteration.
/// <c>run</c> and a connector command that enables telemetry register the OpenTelemetry implementation. Other commands keep the no-op.
/// </summary>
public interface IWorkerIterationObserver
{
    /// <summary>Called when an iteration of <paramref name="workerName"/> starts.</summary>
    /// <param name="workerName">Worker that is starting an iteration.</param>
    void IterationStarting(string workerName);

    /// <summary>Called when an iteration finishes successfully.</summary>
    /// <param name="workerName">Worker that finished.</param>
    /// <param name="duration">How long the iteration ran.</param>
    /// <param name="itemCount">Number of items reported for the iteration.</param>
    void IterationSucceeded(string workerName, TimeSpan duration, int itemCount);

    /// <summary>Called when an iteration fails.</summary>
    /// <param name="workerName">Worker that failed.</param>
    /// <param name="duration">How long the iteration ran before it failed.</param>
    /// <param name="exception">Exception that failed the iteration.</param>
    void IterationFailed(string workerName, TimeSpan duration, Exception exception);

    /// <summary>
    /// The iteration was cancelled. Drop the in-flight span and do not record a failure.
    /// </summary>
    /// <param name="workerName">Worker whose iteration was cancelled.</param>
    void IterationAbandoned(string workerName);
}
