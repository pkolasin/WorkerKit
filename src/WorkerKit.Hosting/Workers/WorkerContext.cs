using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Workers;

/// <summary>
/// Infrastructure services a connector worker used to take one by one.
/// A <see cref="PeriodicWorker"/> or <see cref="QueueWorker{T}"/> takes this object
/// and keeps its own options and clients beside it.
/// </summary>
public sealed class WorkerContext
{
    public WorkerContext(
        IServiceScopeFactory scopes,
        IWorkerHeartbeat heartbeat,
        IWorkerIterationObserver observer,
        ILoggerFactory loggerFactory,
        WorkerTelemetry? telemetry = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(heartbeat);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        Scopes = scopes;
        Heartbeat = heartbeat;
        Observer = observer;
        LoggerFactory = loggerFactory;
        Telemetry = telemetry;
        Time = time ?? TimeProvider.System;
    }

    public IServiceScopeFactory Scopes { get; }

    public IWorkerHeartbeat Heartbeat { get; }

    public IWorkerIterationObserver Observer { get; }

    public ILoggerFactory LoggerFactory { get; }

    /// <summary>Present when the command enabled telemetry. Null for <c>check-config</c> and commands that did not opt in.</summary>
    public WorkerTelemetry? Telemetry { get; }

    /// <summary>Clock for worker delays. The host registers <see cref="TimeProvider.System"/> unless a test replaces it.</summary>
    public TimeProvider Time { get; }

    public ILogger CreateLogger(Type category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return LoggerFactory.CreateLogger(category);
    }

    public ILogger<TCategory> CreateLogger<TCategory>() => LoggerFactory.CreateLogger<TCategory>();
}
