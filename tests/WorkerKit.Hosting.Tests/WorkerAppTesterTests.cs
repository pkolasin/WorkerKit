using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WorkerKit.Abstractions.Commands;
using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Queues;
using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Queues;
using WorkerKit.Hosting.Workers;
using WorkerKit.Testing;

namespace WorkerKit.Hosting.Tests;

public sealed class WorkerAppTesterTests
{
    [Fact]
    public async Task RunIteration_returns_one_pass_and_uses_the_fake_clock()
    {
        var calls = 0;
        await using var tester = WorkerAppTester.Create<WorkerAppTesterTests>()
            .ConfigureServices(services => services.AddSingleton<IHostedService>(sp =>
                new TickWorker(sp.GetRequiredService<WorkerContext>(), () => Interlocked.Increment(ref calls))))
            .Build();

        var result = await tester.RunIterationAsync<TickWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, result.Status);
        Assert.Equal("tick", result.WorkerName);
        Assert.Equal(1, result.ItemCount);
        Assert.Null(result.Error);
        Assert.Equal(1, calls);
        Assert.Same(tester.Time, tester.Services.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public async Task Non_positive_interval_throws_the_hosted_loop_message()
    {
        await using var tester = WorkerAppTester.Create<WorkerAppTesterTests>()
            .ConfigureServices(services => services.AddHostedService<ZeroIntervalWorker>())
            .Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => tester.RunIterationAsync<ZeroIntervalWorker>());

        Assert.Equal("Worker 'zero' interval must be positive.", exception.Message);
    }

    [Fact]
    public async Task Periodic_loop_delay_follows_the_fake_clock()
    {
        var calls = 0;
        await using var tester = WorkerAppTester.Create<WorkerAppTesterTests>()
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(2));
                services.AddSingleton<IHostedService>(sp =>
                    new TickWorker(sp.GetRequiredService<WorkerContext>(), () => Interlocked.Increment(ref calls)));
            })
            .Build();

        await tester.Host.StartAsync();
        await WaitUntil(() => Volatile.Read(ref calls) >= 1);
        Assert.Equal(1, Volatile.Read(ref calls));

        // Advance until the idle delay is released. One jump can land before the timer is armed.
        for (var step = 0; step < 4 && Volatile.Read(ref calls) < 2; step++)
        {
            tester.Time.Advance(TimeSpan.FromHours(1));
            await Task.Delay(30);
        }

        await tester.Host.StopAsync();
        Assert.True(Volatile.Read(ref calls) >= 2);
    }

    [Fact]
    public async Task RunCommand_captures_help_and_a_connector_command()
    {
        await using var tester = WorkerAppTester.Create<WorkerAppTesterTests>()
            .ConfigureApp(app => app.Commands.Add<EchoCommand>())
            .Build();

        var help = await tester.RunCommandAsync("help");
        var echo = await tester.RunCommandAsync("echo");

        Assert.Equal(ExitCodes.Success, help.ExitCode);
        Assert.Contains("echo", help.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(4, echo.ExitCode);
        Assert.Contains("echoed", echo.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("echo-error", echo.StandardError, StringComparison.Ordinal);
        Assert.Contains("ConsoleLifetime", echo.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowsServiceLifetime", echo.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemdLifetime", echo.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hosted_loop_does_not_iterate_when_disabled()
    {
        var calls = 0;
        var starts = 0;
        await using var tester = WorkerAppTester.Create<WorkerAppTesterTests>()
            .ConfigureServices(services => services.AddSingleton<IHostedService>(sp =>
                new DisabledTickWorker(
                    sp.GetRequiredService<WorkerContext>(),
                    () => Interlocked.Increment(ref calls),
                    () => Interlocked.Increment(ref starts))))
            .Build();

        await tester.Host.StartAsync();
        Assert.Equal(0, Volatile.Read(ref calls));
        Assert.Equal(0, Volatile.Read(ref starts));
        Assert.DoesNotContain(tester.Services.GetRequiredService<IWorkerHeartbeat>().Snapshot(), beat => beat.Name == "tick");
        await tester.Host.StopAsync();

        var first = await tester.RunIterationAsync<DisabledTickWorker>();
        var second = await tester.RunIterationAsync<DisabledTickWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, first.Status);
        Assert.Equal(WorkerIterationStatus.Succeeded, second.Status);
        Assert.Equal(2, calls);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task Queue_batch_dead_letters_and_a_disabled_worker_does_not()
    {
        var queue = new InMemoryWorkQueue<string>(TimeProvider.System, "letters");
        queue.Enqueue("bad");
        await using var tester = WorkerAppTester.Create<WorkerAppTesterTests>()
            .ConfigureServices(services => AddLetters(services, queue, enabled: true))
            .Build();

        var result = await tester.RunBatchAsync<LetterWorker>();
        await tester.RunBatchAsync<LetterWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, result.Status);
        Assert.Equal("no", Assert.Single(InMemoryQueue.DeadLetters<string>(tester.Services)).Reason);
        Assert.Equal(0, InMemoryQueue.Pending<string>(tester.Services));
        Assert.Equal(1, OneHosted<LetterWorker>(tester).Starts);

        var held = new InMemoryWorkQueue<string>(TimeProvider.System, "letters");
        held.Enqueue("stay");
        await using var disabled = WorkerAppTester.Create<WorkerAppTesterTests>()
            .ConfigureServices(services => AddLetters(services, held, enabled: false))
            .Build();

        await disabled.Host.StartAsync();
        Assert.Empty(InMemoryQueue.DeadLetters<string>(disabled.Services));
        Assert.Equal(1, InMemoryQueue.Pending<string>(disabled.Services));
        Assert.Equal(0, OneHosted<LetterWorker>(disabled).Starts);
        Assert.DoesNotContain(
            disabled.Services.GetRequiredService<IWorkerHeartbeat>().Snapshot(),
            beat => beat.Name == "letters");
        await disabled.Host.StopAsync();

        var ran = await disabled.RunBatchAsync<LetterWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, ran.Status);
        Assert.Equal("no", Assert.Single(InMemoryQueue.DeadLetters<string>(disabled.Services)).Reason);
        Assert.Equal(0, InMemoryQueue.Pending<string>(disabled.Services));
        Assert.Equal(1, OneHosted<LetterWorker>(disabled).Starts);
    }

    [Fact]
    public void Cli_reference_mismatch_names_the_regenerate_command_and_update_rewrites()
    {
        var path = Path.Combine(Path.GetTempPath(), "workerkit-cli-" + Guid.NewGuid().ToString("n") + ".md");
        try
        {
            File.WriteAllText(path, "stale\r\n");
            var exception = Assert.Throws<WorkerKitAssertException>(() =>
                CliReference.AssertUpToDate(path, "fresh\n", "regen-me"));
            Assert.Contains("regen-me", exception.Message, StringComparison.Ordinal);
            Assert.Contains(CliReference.UpdateVariable, exception.Message, StringComparison.Ordinal);

            CliReference.AssertUpToDate(path, "fresh\r\n", "regen-me", update: true);
            Assert.Equal("fresh\n", File.ReadAllText(path));

            var previous = Environment.GetEnvironmentVariable(CliReference.UpdateVariable);
            Environment.SetEnvironmentVariable(CliReference.UpdateVariable, "1");
            try
            {
                CliReference.AssertUpToDate(path, "from-env\n", "regen-me");
                Assert.Equal("from-env\n", File.ReadAllText(path));
            }
            finally
            {
                Environment.SetEnvironmentVariable(CliReference.UpdateVariable, previous);
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void AddLetters(IServiceCollection services, InMemoryWorkQueue<string> queue, bool enabled)
    {
        services.AddSingleton(queue);
        services.AddSingleton<IWorkQueue<string>>(queue);
        services.AddSingleton<IWorkItemHandler<string>>(new FailHandler());
        services.AddOptions<QueueWorkerOptions>("letters").Configure(options =>
        {
            options.Enabled = enabled;
            options.BatchSize = 10;
            options.MaxConcurrency = 1;
            options.LeaseDuration = TimeSpan.FromMinutes(1);
            options.MaxAttempts = 1;
            options.RetryDelay = TimeSpan.Zero;
            options.MinIdleDelay = TimeSpan.FromMilliseconds(20);
            options.MaxIdleDelay = TimeSpan.FromMilliseconds(20);
        });
        services.AddSingleton<IHostedService>(sp => ActivatorUtilities.CreateInstance<LetterWorker>(sp));
    }

    private static TWorker OneHosted<TWorker>(WorkerAppTester tester)
        where TWorker : class, IHostedService
    {
        var matches = tester.Services.GetServices<IHostedService>().OfType<TWorker>().ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException($"Expected one {typeof(TWorker).Name} hosted service, found {matches.Length}.");
    }

    private static async Task WaitUntil(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed class TickWorker : PeriodicWorker
    {
        private readonly Action _tick;

        public TickWorker(WorkerContext context, Action tick)
            : base(context)
        {
            _tick = tick;
        }

        protected override string WorkerName => "tick";

        protected override TimeSpan Interval => TimeSpan.FromHours(1);

        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _tick();
            return Task.FromResult(1);
        }
    }

    private sealed class DisabledTickWorker : PeriodicWorker
    {
        private readonly Action _tick;
        private readonly Action _start;

        public DisabledTickWorker(WorkerContext context, Action tick, Action start)
            : base(context)
        {
            _tick = tick;
            _start = start;
        }

        protected override string WorkerName => "tick";

        protected override TimeSpan Interval => TimeSpan.FromHours(1);

        protected override bool IsEnabled => false;

        protected override Task OnStartingAsync(CancellationToken cancellationToken)
        {
            _start();
            return base.OnStartingAsync(cancellationToken);
        }

        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _tick();
            return Task.FromResult(1);
        }
    }

    private sealed class ZeroIntervalWorker : PeriodicWorker
    {
        public ZeroIntervalWorker(WorkerContext context)
            : base(context)
        {
        }

        protected override string WorkerName => "zero";

        protected override TimeSpan Interval => TimeSpan.Zero;

        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }

    private sealed class LetterWorker : QueueWorker<string>
    {
        public int Starts;

        public LetterWorker(WorkerContext context, IWorkQueue<string> queue, IOptionsMonitor<QueueWorkerOptions> options)
            : base(context, queue, options)
        {
        }

        protected override string WorkerName => "letters";

        protected override Task OnStartingAsync(CancellationToken cancellationToken)
        {
            Starts++;
            return base.OnStartingAsync(cancellationToken);
        }
    }

    private sealed class FailHandler : IWorkItemHandler<string>
    {
        public Task HandleAsync(WorkItem<string> item, CancellationToken cancellationToken) =>
            throw new PermanentWorkItemException("no");
    }

    private sealed class EchoCommand : IWorkerCommand
    {
        private readonly ICommandOutput _output;
        private readonly IReadOnlyList<IHostLifetime> _lifetimes;

        public EchoCommand(ICommandOutput output, IEnumerable<IHostLifetime> lifetimes)
        {
            _output = output;
            _lifetimes = lifetimes.ToArray();
        }

        public static CommandSpec Spec { get; } = new("echo", "Echo for the tester.");

        static bool IWorkerCommand.ValidateOptions => false;

        public Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        {
            _output.WriteLine("echoed");
            foreach (var lifetime in _lifetimes)
            {
                _output.WriteLine(lifetime.GetType().FullName);
            }

            _output.WriteError("echo-error");
            return Task.FromResult(4);
        }
    }
}
