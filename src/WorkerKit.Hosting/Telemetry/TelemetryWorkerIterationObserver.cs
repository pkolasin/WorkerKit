using System.Diagnostics;

using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Workers;
namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Records one span and the iteration metrics for each <c>PeriodicWorker</c> loop.
/// The span is stored in <see cref="AsyncLocal{T}"/> so it stays the parent of HttpClient spans
/// across the iteration's awaits.
/// </summary>
public sealed class TelemetryWorkerIterationObserver : IWorkerIterationObserver
{
    private readonly WorkerTelemetry _telemetry;
    private readonly AsyncLocal<Activity?> _activity = new();

    public TelemetryWorkerIterationObserver(WorkerTelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    public void IterationStarting(string workerName)
    {
        var activity = WorkerTelemetry.Source.StartActivity($"{workerName} iteration");
        activity?.SetTag("worker", workerName);
        _activity.Value = activity;
    }

    public void IterationSucceeded(string workerName, TimeSpan duration, int itemCount)
    {
        _telemetry.RecordIteration(workerName, "success", duration, itemCount);
        var activity = TakeActivity();
        activity?.SetTag("worker.items", itemCount);
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.Dispose();
    }

    public void IterationFailed(string workerName, TimeSpan duration, Exception exception)
    {
        _telemetry.RecordIteration(workerName, "failure", duration, 0);
        var activity = TakeActivity();
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.Dispose();
    }

    public void IterationAbandoned(string workerName)
    {
        TakeActivity()?.Dispose();
    }

    private Activity? TakeActivity()
    {
        var activity = _activity.Value;
        _activity.Value = null;
        return activity;
    }
}
