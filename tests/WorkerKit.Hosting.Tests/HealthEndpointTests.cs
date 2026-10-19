using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting;

using WorkerKit.Abstractions.Workers;
namespace WorkerKit.Hosting.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Live_and_ready_return_json_and_degraded_fails_only_readiness()
    {
        var port = FreePort();
        var listenUrl = $"http://127.0.0.1:{port}/";
        var install = Directory.CreateTempSubdirectory("ws-health-install").FullName;
        var instanceDir = Directory.CreateTempSubdirectory("ws-health-instance").FullName;
        var heartbeat = new ScriptedHeartbeat();
        var now = DateTimeOffset.UtcNow;
        heartbeat.Items =
        [
            new WorkerHeartbeatSnapshot(
                "example-poll",
                now,
                now,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(15),
                0,
                null),
        ];

        using var host = WorkerHostBuilder.Create(
            ["run", "--instance-dir", instanceDir, "--instance", "probe1"],
            contentRoot: install,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Health:ListenUrl"] = listenUrl,
                    ["Worker:Enabled"] = "false",
                });
                builder.Services.RemoveAll<IWorkerHeartbeat>();
                builder.Services.AddSingleton<IWorkerHeartbeat>(heartbeat);
            }).Build();

        await host.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(listenUrl), Timeout = TimeSpan.FromSeconds(5) };
            var live = await GetWhenListening(client, "/live");
            var ready = await client.GetAsync("/ready");
            var all = await client.GetAsync("/");
            var missing = await client.GetAsync("/nope");

            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            Assert.Equal(HttpStatusCode.OK, all.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Contains("application/json", live.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);

            using var liveJson = JsonDocument.Parse(await live.Content.ReadAsStringAsync());
            AssertShape(liveJson.RootElement, "Healthy", ready: false);
            using var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());
            AssertShape(readyJson.RootElement, "Healthy", ready: true);
            using var allJson = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
            Assert.Equal("Healthy", allJson.RootElement.GetProperty("status").GetString());
            Assert.True(allJson.RootElement.GetProperty("entries").TryGetProperty("self", out _));
            Assert.True(allJson.RootElement.GetProperty("entries").TryGetProperty("instance-dirs", out _));

            heartbeat.Items =
            [
                new WorkerHeartbeatSnapshot(
                    "example-poll",
                    now.AddMinutes(-2),
                    now.AddMinutes(-1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15),
                    3,
                    "ares timeout"),
            ];
            var degradedLive = await client.GetAsync("/live");
            var degradedReady = await client.GetAsync("/ready");
            Assert.Equal(HttpStatusCode.OK, degradedLive.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, degradedReady.StatusCode);
            using var degradedReadyJson = JsonDocument.Parse(await degradedReady.Content.ReadAsStringAsync());
            Assert.Equal("Degraded", degradedReadyJson.RootElement.GetProperty("status").GetString());
            var worker = degradedReadyJson.RootElement
                .GetProperty("entries")
                .GetProperty("workers")
                .GetProperty("data")
                .GetProperty("example-poll");
            Assert.Equal("Degraded", worker.GetProperty("status").GetString());
            Assert.Equal(3, worker.GetProperty("consecutiveFailures").GetInt32());
            Assert.Equal("ares timeout", worker.GetProperty("lastError").GetString());

            heartbeat.Items =
            [
                new WorkerHeartbeatSnapshot(
                    "example-poll",
                    now.AddHours(-2),
                    now.AddHours(-1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15),
                    0,
                    null),
            ];
            var unhealthyLive = await client.GetAsync("/live");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unhealthyLive.StatusCode);
            using var unhealthyJson = JsonDocument.Parse(await unhealthyLive.Content.ReadAsStringAsync());
            Assert.Equal("Unhealthy", unhealthyJson.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public void Check_config_prints_the_health_endpoints_and_the_grace_settings()
    {
        var output = new RecordingOutput();
        var root = Directory.CreateTempSubdirectory("ws-health-check").FullName;
        using var host = WorkerHostBuilder.Create(
            [],
            contentRoot: root,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Health:ListenUrl"] = "http://127.0.0.1:5088/health/",
                    ["Health:HeartbeatStaleMultiplier"] = "3",
                    ["Health:HeartbeatGracePadding"] = "00:05:00",
                });
                builder.Services.AddSingleton<ICommandOutput>(output);
            }).Build();

        var code = host.Services.GetRequiredService<CheckConfigHandler>().Execute();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(output.Lines, line => line == "Health:HeartbeatStaleMultiplier = 3");
        Assert.Contains(output.Lines, line => line == "Health:HeartbeatGracePadding = 00:05:00");
        Assert.Contains(
            output.Lines,
            line => line == "Health endpoints: http://127.0.0.1:5088/health/ (all), http://127.0.0.1:5088/health/live (liveness), http://127.0.0.1:5088/health/ready (readiness)");
        Assert.Contains(output.Lines, line => line.Contains("Data store readiness: not configured", StringComparison.Ordinal));
    }

    [Fact]
    public void Non_loopback_listen_url_fails_validation()
    {
        using var host = WorkerHostBuilder.Create(
            [],
            contentRoot: Directory.CreateTempSubdirectory("ws-health-bind").FullName,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Health:ListenUrl"] = "http://0.0.0.0:5088/health/",
                });
            }).Build();

        var failures = StartupValidation.GetFailures(host.Services);

        Assert.Contains(failures, failure => failure.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    private static void AssertShape(JsonElement root, string status, bool ready)
    {
        Assert.Equal(status, root.GetProperty("status").GetString());
        Assert.Equal("probe1", root.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("version").GetString()));
        var entries = root.GetProperty("entries");
        var workers = entries.GetProperty("workers");
        Assert.Equal(status, workers.GetProperty("status").GetString());
        Assert.Equal("worker heartbeats", workers.GetProperty("description").GetString());
        Assert.False(string.IsNullOrWhiteSpace(workers.GetProperty("duration").GetString()));
        Assert.True(workers.GetProperty("data").TryGetProperty("example-poll", out _));
        Assert.Equal(!ready, entries.TryGetProperty("self", out _));
        Assert.Equal(ready, entries.TryGetProperty("instance-dirs", out _));
        Assert.Equal(ready, entries.TryGetProperty("data-store", out _));
        if (ready)
        {
            Assert.Equal("Healthy", entries.GetProperty("data-store").GetProperty("status").GetString());
            Assert.Contains(
                "not configured",
                entries.GetProperty("data-store").GetProperty("description").GetString(),
                StringComparison.Ordinal);
        }
    }

    private static async Task<HttpResponseMessage> GetWhenListening(HttpClient client, string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                return await client.GetAsync(path);
            }
            catch (HttpRequestException exception)
            {
                last = exception;
                await Task.Delay(50);
            }
        }

        throw new InvalidOperationException("Health endpoint did not accept a connection.", last);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class ScriptedHeartbeat : IWorkerHeartbeat
    {
        public WorkerHeartbeatSnapshot[] Items { get; set; } = [];

        public void Register(string workerName, TimeSpan interval)
        {
        }

        public void Success(string workerName)
        {
        }

        public void Failure(string workerName, Exception exception)
        {
        }

        public IReadOnlyList<WorkerHeartbeatSnapshot> Snapshot() => Items;
    }
}
