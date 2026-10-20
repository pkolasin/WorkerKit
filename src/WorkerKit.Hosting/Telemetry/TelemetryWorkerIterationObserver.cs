using System.Diagnostics;

using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Workers;
namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// Records one span and the iteration metrics for each <c>PeriodicWorker</c> loop.
/// The span is stored in <see cref="AsyncLocal{T}"/> so it stays the parent of HttpClient spans
/// across the iteration's awaits.
/// </summary>
/// <remarks>
/// <see cref="WorkerTelemetry.Source"/> returns no activity when nothing is listening. The metrics are still recorded.
/// The host registers this observer from <c>AddWorkerDefaults</c>. <see cref="NoOpWorkerIterationObserver"/> records nothing.
/// </remarks>
public sealed class TelemetryWorkerIterationObserver : IWorkerIterationObserver
{
    private readonly WorkerTelemetry _telemetry;
    private readonly AsyncLocal<Activity?> _activity = new();

    /// <summary>Stores the telemetry used for iteration metrics. Spans are started on <see cref="WorkerTelemetry.Source"/>.</summary>
    /// <param name="telemetry">Receives the success and failure measurements.</param>
    public TelemetryWorkerIterationObserver(WorkerTelemetry telemetry)
    {
        _telemetry = telemetry;
    }

    /// <summary>
    /// Starts an activity named <c>{workerName} iteration</c>, tags it with <paramref name="workerName"/>, and keeps it for this async flow.
    /// </summary>
    /// <param name="workerName">Worker name written to the activity name and the <c>worker</c> tag.</param>
    public void IterationStarting(string workerName)
    {
        var activity = WorkerTelemetry.Source.StartActivity($"{workerName} iteration");
        activity?.SetTag("worker", workerName);
        _activity.Value = activity;
    }

    /// <summary>
    /// Records a <c>success</c> iteration, tags the activity with <paramref name="itemCount"/>, sets its status to OK, and disposes it.
    /// </summary>
    /// <param name="workerName">Worker name passed to <see cref="WorkerTelemetry.RecordIteration"/>.</param>
    /// <param name="duration">Iteration duration. <see cref="WorkerTelemetry.RecordIteration"/> stores it in seconds.</param>
    /// <param name="itemCount">Stored on the activity as <c>worker.items</c> and passed to <see cref="WorkerTelemetry.RecordIteration"/>.</param>
    public void IterationSucceeded(string workerName, TimeSpan duration, int itemCount)
    {
        _telemetry.RecordIteration(workerName, "success", duration, itemCount);
        var activity = TakeActivity();
        activity?.SetTag("worker.items", itemCount);
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.Dispose();
    }

    /// <summary>
    /// Records a <c>failure</c> iteration with an item count of zero, adds <paramref name="exception"/> to the activity, sets its status to error, and disposes it.
    /// </summary>
    /// <param name="workerName">Worker name passed to <see cref="WorkerTelemetry.RecordIteration"/>.</param>
    /// <param name="duration">Iteration duration. <see cref="WorkerTelemetry.RecordIteration"/> stores it in seconds.</param>
    /// <param name="exception">Exception added to the activity. Its message is the activity status description.</param>
    public void IterationFailed(string workerName, TimeSpan duration, Exception exception)
    {
        _telemetry.RecordIteration(workerName, "failure", duration, 0);
        var activity = TakeActivity();
        activity?.AddException(exception);
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.Dispose();
    }

    /// <summary>Disposes the in-flight activity. No iteration metric is recorded.</summary>
    /// <param name="workerName">Unused. The activity was stored by <see cref="IterationStarting"/>.</param>
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
