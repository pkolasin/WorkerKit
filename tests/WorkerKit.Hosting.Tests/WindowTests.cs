using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerKit.Abstractions.Workers;
using WorkerKit.Hosting.Workers;
using WorkerKit.Testing;
using ScheduleWindow = WorkerKit.Hosting.Workers.Window;

namespace WorkerKit.Hosting.Tests;

public sealed class WindowTests
{
    [Fact]
    public void Daily_utc_window_includes_the_current_instant_and_otherwise_moves_to_the_next_day()
    {
        var window = ScheduleWindow.DailyAt(new TimeOnly(3, 15), TimeZoneInfo.Utc);

        Assert.Equal(
            new DateTimeOffset(2026, 1, 2, 3, 15, 0, TimeSpan.Zero),
            window.NextOccurrence(new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero)));
        Assert.Equal(
            new DateTimeOffset(2026, 1, 2, 3, 15, 0, TimeSpan.Zero),
            window.NextOccurrence(new DateTimeOffset(2026, 1, 2, 3, 15, 0, TimeSpan.Zero)));
        Assert.Equal(
            new DateTimeOffset(2026, 1, 3, 3, 15, 0, TimeSpan.Zero),
            window.NextOccurrence(new DateTimeOffset(2026, 1, 2, 3, 16, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Spring_forward_gap_moves_to_the_first_valid_minute()
    {
        var window = ScheduleWindow.DailyAt(new TimeOnly(2, 30), EasternLike());
        var next = window.NextOccurrence(new DateTimeOffset(2026, 3, 8, 6, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 3, 8, 7, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void Ambiguous_local_time_uses_the_earlier_offset_and_then_the_later_one()
    {
        var window = ScheduleWindow.DailyAt(new TimeOnly(1, 30), EasternLike());
        var earlier = new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero);

        Assert.Equal(earlier, window.NextOccurrence(new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero)));
        Assert.Equal(earlier, window.NextOccurrence(earlier));
        Assert.Equal(later, window.NextOccurrence(new DateTimeOffset(2026, 11, 1, 5, 45, 0, TimeSpan.Zero)));
        Assert.Equal(later, window.NextOccurrence(later));
        Assert.Equal(later.AddDays(1), window.NextOccurrence(later.AddMinutes(1)));
    }

    [Fact]
    public async Task RunIteration_does_not_wait_for_the_window()
    {
        var calls = 0;
        var builder = WorkerAppTester.Create<WindowTests>()
            .ConfigureServices(services => services.AddSingleton<IHostedService>(sp =>
                new NightlyWorker(sp.GetRequiredService<WorkerContext>(), () => Interlocked.Increment(ref calls))));
        builder.Time.SetUtcNow(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        await using var tester = builder.Build();

        var result = await tester.RunIterationAsync<NightlyWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, result.Status);
        Assert.Equal(1, calls);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), tester.Time.GetUtcNow());
    }

    [Fact]
    public async Task Hosted_loop_waits_until_the_daily_window_and_registers_the_gap()
    {
        var calls = 0;
        var builder = WorkerAppTester.Create<WindowTests>()
            .ConfigureServices(services =>
            {
                services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(2));
                services.AddSingleton<IHostedService>(sp =>
                    new NightlyWorker(sp.GetRequiredService<WorkerContext>(), () => Interlocked.Increment(ref calls)));
            });
        builder.Time.SetUtcNow(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));
        await using var tester = builder.Build();

        await tester.Host.StartAsync();
        await Task.Delay(150);
        Assert.Equal(0, Volatile.Read(ref calls));
        var beat = tester.Services.GetRequiredService<IWorkerHeartbeat>().Snapshot().Single(item => item.Name == "nightly");
        Assert.Equal(TimeSpan.FromHours(24), beat.Interval);

        for (var step = 0; step < 4 && Volatile.Read(ref calls) < 1; step++)
        {
            tester.Time.Advance(TimeSpan.FromHours(4));
            await Task.Delay(30);
        }

        await WaitUntil(() => Volatile.Read(ref calls) >= 1);
        await Task.Delay(150);
        Assert.Equal(1, Volatile.Read(ref calls));

        for (var step = 0; step < 4 && Volatile.Read(ref calls) < 2; step++)
        {
            tester.Time.Advance(TimeSpan.FromHours(24));
            await Task.Delay(30);
        }

        await WaitUntil(() => Volatile.Read(ref calls) >= 2);
        await tester.Host.StopAsync();
    }

    private static TimeZoneInfo EasternLike()
    {
        var start = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            month: 3,
            week: 2,
            DayOfWeek.Sunday);
        var end = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            new DateTime(1, 1, 1, 2, 0, 0),
            month: 11,
            week: 1,
            DayOfWeek.Sunday);
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2000, 1, 1),
            DateTime.MaxValue.Date,
            TimeSpan.FromHours(1),
            start,
            end);
        return TimeZoneInfo.CreateCustomTimeZone(
            "WorkerKit-Test-Eastern",
            TimeSpan.FromHours(-5),
            "WorkerKit Test Eastern",
            "WorkerKit Test Eastern Standard",
            "WorkerKit Test Eastern Daylight",
            [rule]);
    }

    private static async Task WaitUntil(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed class NightlyWorker : PeriodicWorker
    {
        private static readonly ScheduleWindow Nightly = ScheduleWindow.DailyAt(new TimeOnly(4, 0), TimeZoneInfo.Utc);
        private readonly Action _tick;

        public NightlyWorker(WorkerContext context, Action tick)
            : base(context)
        {
            _tick = tick;
        }

        protected override string WorkerName => "nightly";

        protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

        protected override ScheduleWindow? Window => Nightly;

        protected override Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            _tick();
            return Task.FromResult(1);
        }
    }
}
