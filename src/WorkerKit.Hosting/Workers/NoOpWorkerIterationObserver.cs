
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Workers;

/// <summary>
/// Default <see cref="IWorkerIterationObserver"/>. <c>run</c> and a connector command that enables telemetry replace it with the OpenTelemetry observer.
/// </summary>
public sealed class NoOpWorkerIterationObserver : IWorkerIterationObserver
{
    public void IterationStarting(string workerName)
    {
    }

    public void IterationSucceeded(string workerName, TimeSpan duration, int itemCount)
    {
    }

    public void IterationFailed(string workerName, TimeSpan duration, Exception exception)
    {
    }

    public void IterationAbandoned(string workerName)
    {
    }
}
