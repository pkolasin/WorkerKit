
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Workers;

/// <summary>
/// Default <see cref="IWorkerIterationObserver"/>. <c>run</c> and a connector command that enables telemetry replace it with the OpenTelemetry observer.
/// </summary>
/// <remarks>Every method returns immediately. No span is started and no metric is recorded.</remarks>
public sealed class NoOpWorkerIterationObserver : IWorkerIterationObserver
{
    /// <summary>Does nothing.</summary>
    /// <param name="workerName">Ignored.</param>
    public void IterationStarting(string workerName)
    {
    }

    /// <summary>Does nothing.</summary>
    /// <param name="workerName">Ignored.</param>
    /// <param name="duration">Ignored.</param>
    /// <param name="itemCount">Ignored.</param>
    public void IterationSucceeded(string workerName, TimeSpan duration, int itemCount)
    {
    }

    /// <summary>Does nothing.</summary>
    /// <param name="workerName">Ignored.</param>
    /// <param name="duration">Ignored.</param>
    /// <param name="exception">Ignored.</param>
    public void IterationFailed(string workerName, TimeSpan duration, Exception exception)
    {
    }

    /// <summary>Does nothing.</summary>
    /// <param name="workerName">Ignored.</param>
    public void IterationAbandoned(string workerName)
    {
    }
}
