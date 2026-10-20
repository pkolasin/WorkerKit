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
    /// <summary>
    /// Stores the shared worker services. A null <paramref name="time"/> selects <see cref="TimeProvider.System"/>.
    /// </summary>
    /// <param name="scopes">Scope factory for one iteration or one queue item.</param>
    /// <param name="heartbeat">Heartbeat updated after each iteration.</param>
    /// <param name="observer">Iteration observer. <see cref="NoOpWorkerIterationObserver"/> records nothing.</param>
    /// <param name="loggerFactory">Factory used by <see cref="CreateLogger(Type)"/> and <see cref="CreateLogger{TCategory}"/>.</param>
    /// <param name="telemetry">Metrics object. Null when this command did not enable telemetry.</param>
    /// <param name="time">Clock for worker delays. Null selects <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="scopes"/>, <paramref name="heartbeat"/>, <paramref name="observer"/>, or <paramref name="loggerFactory"/> is null.
    /// </exception>
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

    /// <summary>Scope factory passed to the constructor.</summary>
    public IServiceScopeFactory Scopes { get; }

    /// <summary>Heartbeat passed to the constructor.</summary>
    public IWorkerHeartbeat Heartbeat { get; }

    /// <summary>
    /// Observer passed to the constructor.
    /// <see cref="NoOpWorkerIterationObserver"/> records nothing. The host replaces it with <see cref="TelemetryWorkerIterationObserver"/> when telemetry is enabled.
    /// </summary>
    public IWorkerIterationObserver Observer { get; }

    /// <summary>Logger factory passed to the constructor.</summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>Present when the command enabled telemetry. Null for <c>check-config</c> and commands that did not opt in.</summary>
    public WorkerTelemetry? Telemetry { get; }

    /// <summary>Clock for worker delays. The host registers <see cref="TimeProvider.System"/> unless a test replaces it.</summary>
    public TimeProvider Time { get; }

    /// <summary>Creates a logger for <paramref name="category"/>.</summary>
    /// <param name="category">Logger category type.</param>
    /// <returns>The logger from <see cref="LoggerFactory"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="category"/> is null.</exception>
    public ILogger CreateLogger(Type category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return LoggerFactory.CreateLogger(category);
    }

    /// <summary>Creates a logger whose category is <typeparamref name="TCategory"/>.</summary>
    /// <typeparam name="TCategory">Logger category type.</typeparam>
    /// <returns>The logger from <see cref="LoggerFactory"/>.</returns>
    public ILogger<TCategory> CreateLogger<TCategory>() => LoggerFactory.CreateLogger<TCategory>();
}
