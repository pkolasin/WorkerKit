using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using WorkerKit.Hosting.Health;

using WorkerKit.Abstractions.Data;
using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Tests;

public sealed class HeartbeatHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Grace_is_the_larger_of_the_multiplier_and_the_padding()
    {
        var options = new HealthOptions();

        Assert.Equal(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), options.HeartbeatGraceFor(TimeSpan.FromSeconds(1)));
        Assert.Equal(TimeSpan.FromHours(3), options.HeartbeatGraceFor(TimeSpan.FromHours(1)));
        Assert.Equal(TimeSpan.FromMinutes(30), options.HeartbeatGraceFor(TimeSpan.FromMinutes(10)));
    }

    [Fact]
    public void Health_status_enum_orders_unhealthy_below_degraded_below_healthy()
    {
        Assert.True(HealthStatus.Unhealthy < HealthStatus.Degraded);
        Assert.True(HealthStatus.Degraded < HealthStatus.Healthy);
    }

    [Fact]
    public void Startup_grace_stays_healthy_until_the_first_success_is_due()
    {
        var worker = Snapshot(registered: Now, lastSuccess: null, grace: TimeSpan.FromMinutes(15), failures: 4);

        Assert.Equal(HealthStatus.Healthy, WorkerHeartbeatRules.Evaluate(worker, Now.AddMinutes(15) - TimeSpan.FromTicks(1)));
        Assert.Equal(HealthStatus.Degraded, WorkerHeartbeatRules.Evaluate(worker, Now.AddMinutes(15)));
        Assert.Equal(HealthStatus.Unhealthy, WorkerHeartbeatRules.Evaluate(worker, Now.AddMinutes(15) + TimeSpan.FromTicks(1)));
    }

    [Fact]
    public void Failures_inside_the_grace_window_are_degraded_and_a_stale_success_is_unhealthy()
    {
        var fresh = Snapshot(registered: Now.AddMinutes(-30), lastSuccess: Now.AddMinutes(-1), grace: TimeSpan.FromMinutes(15), failures: 2);
        var stale = Snapshot(registered: Now.AddHours(-2), lastSuccess: Now.AddMinutes(-16), grace: TimeSpan.FromMinutes(15), failures: 0);
        var recovered = fresh with { ConsecutiveFailures = 0, LastError = null };

        Assert.Equal(HealthStatus.Degraded, WorkerHeartbeatRules.Evaluate(fresh, Now));
        Assert.Equal(HealthStatus.Unhealthy, WorkerHeartbeatRules.Evaluate(stale, Now));
        Assert.Equal(HealthStatus.Healthy, WorkerHeartbeatRules.Evaluate(recovered, Now));
    }

    [Fact]
    public async Task Heartbeat_check_reports_the_worst_worker_and_its_data()
    {
        var time = new FixedTime(Now);
        var heartbeat = new FakeHeartbeat(
        [
            Snapshot("fresh", Now, Now, TimeSpan.FromMinutes(15), 0),
            Snapshot("stuck", Now.AddHours(-1), null, TimeSpan.FromMinutes(15), 1),
        ]);
        var check = new WorkerHeartbeatHealthCheck(heartbeat, time);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("worker heartbeats", result.Description);
        Assert.True(result.Data.ContainsKey("fresh"));
        Assert.True(result.Data.ContainsKey("stuck"));
    }

    [Fact]
    public void Registry_computes_grace_from_current_options_and_counts_failures()
    {
        var time = new FixedTime(Now);
        var options = new HealthOptions { HeartbeatStaleMultiplier = 3, HeartbeatGracePadding = TimeSpan.FromMinutes(5) };
        var registry = new WorkerHeartbeatRegistry(new FixedOptions(options), time);

        registry.Register("example-poll", TimeSpan.FromMinutes(10));
        registry.Failure("example-poll", new InvalidOperationException("boom"));
        registry.Failure("example-poll", new InvalidOperationException("boom"));
        var failed = Assert.Single(registry.Snapshot());

        Assert.Equal(TimeSpan.FromMinutes(30), failed.Grace);
        Assert.Equal(2, failed.ConsecutiveFailures);
        Assert.Equal("boom", failed.LastError);
        Assert.Null(failed.LastSuccess);

        time.Now = Now.AddMinutes(1);
        registry.Success("example-poll");
        var recovered = Assert.Single(registry.Snapshot());

        Assert.Equal(Now.AddMinutes(1), recovered.LastSuccess);
        Assert.Equal(0, recovered.ConsecutiveFailures);
        Assert.Null(recovered.LastError);
    }

    [Fact]
    public async Task Missing_instance_directory_is_unhealthy_and_a_writable_pair_is_healthy()
    {
        var root = Directory.CreateTempSubdirectory("ws-health-dirs").FullName;
        var data = Path.Combine(root, "data");
        var temp = Path.Combine(root, "temp");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(temp);
        var healthy = new InstanceDirectoriesHealthCheck(new FakeInstance(data, temp));
        var missing = new InstanceDirectoriesHealthCheck(new FakeInstance(Path.Combine(root, "missing"), temp));

        var ok = await healthy.CheckHealthAsync(new HealthCheckContext());
        var bad = await missing.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, ok.Status);
        Assert.Equal(HealthStatus.Unhealthy, bad.Status);
        Assert.Contains("data directory", bad.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Data_store_check_skips_none_and_fails_when_the_factory_is_missing_or_throws()
    {
        var skipped = await new DataStoreHealthCheck(new FixedOptions(new DataOptions { Provider = "None" }), []).CheckHealthAsync(new HealthCheckContext());
        var missing = await new DataStoreHealthCheck(new FixedOptions(new DataOptions { Provider = "Dapper" }), []).CheckHealthAsync(new HealthCheckContext());
        var broken = await new DataStoreHealthCheck(
            new FixedOptions(new DataOptions { Provider = "Dapper" }),
            [new ThrowingFactory()]).CheckHealthAsync(new HealthCheckContext());
        var answered = await new DataStoreHealthCheck(
            new FixedOptions(new DataOptions { Provider = "Dapper" }),
            [new ProbeFactory()]).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, skipped.Status);
        Assert.Contains("not configured", skipped.Description, StringComparison.Ordinal);
        Assert.Equal(HealthStatus.Unhealthy, missing.Status);
        Assert.Contains("IDbConnectionFactory", missing.Description, StringComparison.Ordinal);
        Assert.Equal(HealthStatus.Unhealthy, broken.Status);
        Assert.Equal(HealthStatus.Healthy, answered.Status);
        Assert.Contains("SELECT 1", answered.Description, StringComparison.Ordinal);
        Assert.Equal("SELECT 1", ProbeFactory.LastCommandText);
    }

    [Fact]
    public void Liveness_accepts_degraded_and_readiness_does_not()
    {
        Assert.Equal(200, HealthResponses.StatusCode("live", HealthStatus.Healthy));
        Assert.Equal(200, HealthResponses.StatusCode("live", HealthStatus.Degraded));
        Assert.Equal(503, HealthResponses.StatusCode("live", HealthStatus.Unhealthy));
        Assert.Equal(200, HealthResponses.StatusCode("all", HealthStatus.Degraded));
        Assert.Equal(503, HealthResponses.StatusCode("all", HealthStatus.Unhealthy));
        Assert.Equal(200, HealthResponses.StatusCode("ready", HealthStatus.Healthy));
        Assert.Equal(503, HealthResponses.StatusCode("ready", HealthStatus.Degraded));
        Assert.Equal(503, HealthResponses.StatusCode("ready", HealthStatus.Unhealthy));
    }

    [Fact]
    public void Routes_follow_the_listen_prefix()
    {
        const string prefixed = "http://127.0.0.1:5088/health/";
        Assert.Equal("all", HealthResponses.Match(prefixed, "/health/"));
        Assert.Equal("live", HealthResponses.Match(prefixed, "/health/live"));
        Assert.Equal("ready", HealthResponses.Match(prefixed, "/health/ready/"));
        Assert.Null(HealthResponses.Match(prefixed, "/health/other"));

        const string root = "http://127.0.0.1:5088/";
        Assert.Equal("all", HealthResponses.Match(root, "/"));
        Assert.Equal("live", HealthResponses.Match(root, "/live"));
        Assert.Equal("ready", HealthResponses.Match(root, "/ready"));
    }

    private static WorkerHeartbeatSnapshot Snapshot(
        DateTimeOffset registered,
        DateTimeOffset? lastSuccess,
        TimeSpan grace,
        int failures)
    {
        return Snapshot("worker", registered, lastSuccess, grace, failures);
    }

    private static WorkerHeartbeatSnapshot Snapshot(
        string name,
        DateTimeOffset registered,
        DateTimeOffset? lastSuccess,
        TimeSpan grace,
        int failures)
    {
        return new WorkerHeartbeatSnapshot(
            name,
            registered,
            lastSuccess,
            TimeSpan.FromMinutes(5),
            grace,
            failures,
            failures > 0 ? "boom" : null);
    }

    private sealed class FixedTime : TimeProvider
    {
        public FixedTime(DateTimeOffset now)
        {
            Now = now;
        }

        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedOptions : IOptionsMonitor<HealthOptions>, IOptionsMonitor<DataOptions>
    {
        private readonly HealthOptions _health;
        private readonly DataOptions _data;

        public FixedOptions(HealthOptions health)
        {
            _health = health;
            _data = new DataOptions();
        }

        public FixedOptions(DataOptions data)
        {
            _health = new HealthOptions();
            _data = data;
        }

        HealthOptions IOptionsMonitor<HealthOptions>.CurrentValue => _health;

        DataOptions IOptionsMonitor<DataOptions>.CurrentValue => _data;

        public HealthOptions Get(string? name) => _health;

        DataOptions IOptionsMonitor<DataOptions>.Get(string? name) => _data;

        public IDisposable OnChange(Action<HealthOptions, string?> listener) => EmptyDisposable.Instance;

        IDisposable IOptionsMonitor<DataOptions>.OnChange(Action<DataOptions, string?> listener) => EmptyDisposable.Instance;
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class FakeHeartbeat : IWorkerHeartbeat
    {
        private readonly IReadOnlyList<WorkerHeartbeatSnapshot> _items;

        public FakeHeartbeat(IReadOnlyList<WorkerHeartbeatSnapshot> items)
        {
            _items = items;
        }

        public void Register(string workerName, TimeSpan interval)
        {
        }

        public void Success(string workerName)
        {
        }

        public void Failure(string workerName, Exception exception)
        {
        }

        public IReadOnlyList<WorkerHeartbeatSnapshot> Snapshot() => _items;
    }

    private sealed class FakeInstance : IInstanceContext
    {
        public FakeInstance(string dataDirectory, string tempDirectory)
        {
            DataDirectory = dataDirectory;
            TempDirectory = tempDirectory;
        }

        public string InstanceName => "probe";

        public string InstallDirectory => DataDirectory;

        public string InstanceDirectory => DataDirectory;

        public string LogDirectory => DataDirectory;

        public string DataDirectory { get; }

        public string TempDirectory { get; }

        public string ResolvePath(string path) => path;
    }

    private sealed class ThrowingFactory : IDbConnectionFactory
    {
        public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("database is down");
        }
    }

#nullable disable
    private sealed class ProbeFactory : IDbConnectionFactory
    {
        public static string LastCommandText { get; private set; }

        public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            return new ValueTask<DbConnection>(new ProbeConnection());
        }

        private sealed class ProbeConnection : DbConnection
        {
            private ConnectionState _state = ConnectionState.Closed;

            public override string ConnectionString { get; set; } = "";

            public override string Database => "probe";

            public override string DataSource => "probe";

            public override string ServerVersion => "0";

            public override ConnectionState State => _state;

            public override void ChangeDatabase(string databaseName)
            {
            }

            public override void Close() => _state = ConnectionState.Closed;

            public override void Open() => _state = ConnectionState.Open;

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
                throw new NotSupportedException();

            protected override DbCommand CreateDbCommand() => new ProbeCommand();
        }

        private sealed class ProbeCommand : DbCommand
        {
            public override string CommandText { get; set; } = "";

            public override int CommandTimeout { get; set; }

            public override CommandType CommandType { get; set; }

            public override bool DesignTimeVisible { get; set; }

            public override UpdateRowSource UpdatedRowSource { get; set; }

            protected override DbConnection DbConnection { get; set; }

            protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();

            protected override DbTransaction DbTransaction { get; set; }

            public override void Cancel()
            {
            }

            public override int ExecuteNonQuery() => 1;

            public override object ExecuteScalar()
            {
                LastCommandText = CommandText;
                return 1;
            }

            public override void Prepare()
            {
            }

            protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
                throw new NotSupportedException();
        }
    }
#nullable restore
}
