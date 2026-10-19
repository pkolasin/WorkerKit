using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Queues;
using WorkerKit.Hosting.Telemetry;
using WorkerKit.Testing;

using ExampleHost;
using ExampleHost.Composition;
using WorkerKit.Samples.OpenMeteo;
using WorkerKit.Samples.Outbound.Queue;
using WorkerKit.Samples.Vies.Queue;
using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Queues;
using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Workers;
namespace ExampleHost.Tests;

public sealed class QueueWorkerTests
{
    [Fact]
    public async Task Empty_queue_is_a_successful_heartbeat_and_the_example_stays_disabled()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        await using var tester = StartProbe(queue, new ScriptHandler((_, _) => Task.CompletedTask));

        await tester.RunIterationAsync<ForecastWorker>();
        await tester.RunBatchAsync<ProbeWorker>();

        Assert.Equal(0, queue.PendingCount);
        Assert.Null(Heartbeat(tester, ViesQueueWorker.WorkerNameValue));
        Assert.Null(Heartbeat(tester, OutboundQueueWorker.WorkerNameValue));
        Assert.NotNull(Heartbeat(tester, ForecastWorker.WorkerNameValue));
        Assert.NotNull(Heartbeat(tester, "probe")?.LastSuccess);
    }

    [Fact]
    public async Task Handler_success_completes_the_item()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("hello");
        var handler = new ScriptHandler((_, _) => Task.CompletedTask);
        await using var tester = StartProbe(queue, handler);

        await tester.RunBatchAsync<ProbeWorker>();

        Assert.Equal(1, handler.Calls);
        Assert.Empty(queue.DeadLetters);
        Assert.Empty(await queue.ClaimAsync(1, TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.NotNull(Heartbeat(tester, "probe")?.LastSuccess);
    }

    [Fact]
    public async Task Transient_failure_is_released_and_then_completed()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("hello");
        var failed = false;
        var handler = new ScriptHandler((_, _) =>
        {
            if (!failed)
            {
                failed = true;
                throw new InvalidOperationException("transient");
            }

            return Task.CompletedTask;
        });
        await using var tester = StartProbe(queue, handler);

        await tester.RunBatchAsync<ProbeWorker>();
        await tester.RunBatchAsync<ProbeWorker>();

        Assert.Equal(2, handler.Calls);
        Assert.Empty(queue.DeadLetters);
    }

    [Fact]
    public async Task Permanent_failure_is_dead_lettered_once()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("hello");
        var handler = new ScriptHandler((_, _) => throw new PermanentWorkItemException("nope"));
        await using var tester = StartProbe(queue, handler);

        await tester.RunBatchAsync<ProbeWorker>();

        Assert.Equal(1, handler.Calls);
        Assert.Equal("nope", queue.DeadLetters[0].Reason);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task Attempt_limit_dead_letters_the_item()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("hello");
        var handler = new ScriptHandler((_, _) => throw new InvalidOperationException("still bad"));
        await using var tester = StartProbe(queue, handler, options => options.MaxAttempts = 2);

        await tester.RunBatchAsync<ProbeWorker>();
        await tester.RunBatchAsync<ProbeWorker>();

        Assert.Equal(2, handler.Calls);
        Assert.Equal(2, queue.DeadLetters[0].Attempt);
        Assert.Equal("still bad", queue.DeadLetters[0].Reason);
    }

    [Fact]
    public async Task Claim_failure_fails_the_heartbeat()
    {
        var queue = new ThrowingQueue();
        var handler = new ScriptHandler((_, _) => Task.CompletedTask);
        await using var tester = StartProbe(queue, handler);

        var result = await tester.RunBatchAsync<ProbeWorker>();

        Assert.Equal(WorkerIterationStatus.Failed, result.Status);
        Assert.Equal(0, handler.Calls);
        Assert.Null(Heartbeat(tester, "probe")?.LastSuccess);
        Assert.True(Heartbeat(tester, "probe")?.ConsecutiveFailures >= 1);
    }

    [Fact]
    public async Task Shutdown_drains_in_flight_work_and_releases_the_batch()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("a");
        queue.Enqueue("b");
        var started = 0;
        var finished = 0;
        var handler = new ScriptHandler(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref started);
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                Interlocked.Increment(ref finished);
            }
        });
        await using var tester = StartProbe(queue, handler, options =>
        {
            options.BatchSize = 2;
            options.MaxConcurrency = 1;
        });

        await tester.Host.StartAsync();
        await WaitUntil(() => Volatile.Read(ref started) >= 1);
        await tester.Host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(Volatile.Read(ref started), Volatile.Read(ref finished));
        Assert.InRange(started, 1, 2);
        Assert.Equal(2, queue.PendingCount);
        Assert.Empty(queue.DeadLetters);
    }

    [Fact]
    public async Task Max_concurrency_runs_items_together()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("a");
        queue.Enqueue("b");
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptHandler(async (_, cancellationToken) =>
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                bothEntered.TrySetResult();
            }

            await release.Task.WaitAsync(cancellationToken);
        });
        await using var tester = StartProbe(queue, handler, options =>
        {
            options.BatchSize = 2;
            options.MaxConcurrency = 2;
        });

        var batch = tester.RunBatchAsync<ProbeWorker>();
        try
        {
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            release.TrySetResult();
            await batch.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.Equal(2, handler.Calls);
        Assert.Empty(queue.DeadLetters);
    }

    [Fact]
    public async Task Item_span_is_a_child_of_the_iteration_and_records_outcome()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("hello");
        var stopped = new List<(string Name, ActivityStatusCode Status, ActivitySpanId Id, ActivitySpanId Parent)>();
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkerTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped.Add((activity.DisplayName, activity.Status, activity.SpanId, activity.ParentSpanId)),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MetricBag();
        var handler = new ScriptHandler((_, _) => Task.CompletedTask);
        await using var tester = StartProbe(queue, handler, telemetry: true);

        await tester.RunBatchAsync<ProbeWorker>();

        var item = stopped.Last(activity => activity.Name == "probe item");
        var iteration = stopped.Last(activity => activity.Id == item.Parent);
        Assert.Equal(ActivityStatusCode.Ok, item.Status);
        Assert.Equal("probe iteration", iteration.Name);
        Assert.Contains(metrics.Samples, sample =>
            sample.Name == "worker.items.processed" && sample.Worker == "probe" && sample.Outcome == "done" && sample.Value == 1);
    }

    [Fact]
    public async Task Failed_item_span_is_error_and_pending_gauge_reads_the_queue()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "probe");
        queue.Enqueue("hello");
        var stopped = new List<(string Name, ActivityStatusCode Status)>();
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkerTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped.Add((activity.DisplayName, activity.Status)),
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MetricBag();
        var handler = new ScriptHandler((_, _) => throw new InvalidOperationException("transient"));
        await using var tester = StartProbe(queue, handler, telemetry: true);

        metrics.Listen(tester.Services.GetRequiredService<WorkerTelemetry>());
        metrics.Observe();
        Assert.Contains(metrics.Samples, sample =>
            sample.Name == WorkerTelemetry.QueuePendingName && sample.Worker == "probe" && sample.Value == 1);

        await tester.RunBatchAsync<ProbeWorker>();

        Assert.Contains(metrics.Samples, sample =>
            sample.Name == "worker.items.processed" && sample.Worker == "probe" && sample.Outcome == "retry");
    }

    [Fact]
    public async Task Idle_backoff_longer_than_heartbeat_grace_fails_validation()
    {
        await using var tester = WorkerAppTester.Create<Program>()
            .ConfigureApp(app => app.AddExampleConnector())
            .UseSetting("Queue:vies-queue:MinIdleDelay", "00:00:01")
            .UseSetting("Queue:vies-queue:MaxIdleDelay", "01:00:00")
            .Build();

        var failures = StartupValidation.GetFailures(tester.Services);

        Assert.Contains(failures, failure => failure.Contains("MaxIdleDelay", StringComparison.Ordinal));
    }

    private static WorkerHeartbeatSnapshot? Heartbeat(WorkerAppTester tester, string name) =>
        tester.Services.GetRequiredService<IWorkerHeartbeat>().Snapshot().SingleOrDefault(worker => worker.Name == name);

    private static WorkerAppTester StartProbe(
        IWorkQueue<string> queue,
        ScriptHandler handler,
        Action<QueueWorkerOptions>? configure = null,
        bool telemetry = false)
    {
        return WorkerAppTester.Create<Program>()
            .ConfigureApp(app => app.AddExampleConnector())
            .EnableTelemetry(telemetry)
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(10));
                if (queue is InMemoryWorkQueue<string> memory)
                {
                    services.AddSingleton(memory);
                    services.AddSingleton<IQueuePendingSource>(memory);
                }

                services.AddSingleton(queue);
                services.AddSingleton<IWorkItemHandler<string>>(handler);
                services.AddOptions<QueueWorkerOptions>("probe").Configure(options =>
                {
                    options.Enabled = true;
                    options.BatchSize = 10;
                    options.MaxConcurrency = 1;
                    options.LeaseDuration = TimeSpan.FromMinutes(1);
                    options.MaxAttempts = 5;
                    options.RetryDelay = TimeSpan.Zero;
                    options.MinIdleDelay = TimeSpan.FromMilliseconds(30);
                    options.MaxIdleDelay = TimeSpan.FromMilliseconds(80);
                    configure?.Invoke(options);
                });
                services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<ProbeWorker>(sp));
            })
            .Build();
    }

    private static async Task WaitUntil(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed class ProbeWorker : QueueWorker<string>
    {
        public ProbeWorker(
            WorkerContext context,
            IWorkQueue<string> queue,
            IOptionsMonitor<QueueWorkerOptions> options)
            : base(context, queue, options)
        {
        }

        protected override string WorkerName => "probe";
    }

    private sealed class ScriptHandler(Func<WorkItem<string>, CancellationToken, Task> handle) : IWorkItemHandler<string>
    {
        public int Calls;

        public Task HandleAsync(WorkItem<string> item, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return handle(item, cancellationToken);
        }
    }

    private sealed class ThrowingQueue : IWorkQueue<string>
    {
        public Task<IReadOnlyList<WorkItem<string>>> ClaimAsync(int max, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("queue down");

        public Task CompleteAsync(WorkItem<string> item, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(WorkItem<string> item, TimeSpan? delay, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task FailAsync(WorkItem<string> item, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MetricBag : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<Sample> Samples { get; } = [];

        public MetricBag()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == WorkerTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Samples.Add(new Sample(instrument.Name, value, Tag(tags, "worker"), Tag(tags, "outcome"))));
            _listener.Start();
        }

        public void Listen(WorkerTelemetry telemetry)
        {
            _ = telemetry;
        }

        public void Observe() => _listener.RecordObservableInstruments();

        public void Dispose() => _listener.Dispose();

        private static string? Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == key)
                {
                    return tag.Value?.ToString();
                }
            }

            return null;
        }
    }

    private sealed record Sample(string Name, long Value, string? Worker, string? Outcome);
}
