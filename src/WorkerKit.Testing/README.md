# WorkerKit.Testing

Test helpers for a connector that uses `WorkerKit.Hosting`. The package does not reference xUnit, NUnit, or MSTest. A failed check throws `WorkerKitAssertException`.

`WorkerAppTester` builds the host from the same `ConfigureHost` delegates as production:

- in-memory configuration overrides
- a temporary instance directory, deleted on dispose
- `FakeTimeProvider` registered as `TimeProvider` (`PeriodicWorker` delays use `WorkerContext.Time`). `WorkerAppTester.Time` returns that type, and `Microsoft.Extensions.TimeProvider.Testing` flows to consumers as a normal dependency
- a `ConfigureServices` hook that runs after the connector, so replacements win
- no OpenTelemetry export unless `EnableTelemetry` is set, and then only when an OTLP endpoint is configured
- no Windows Service or systemd lifetime

The host is not started. `RunIterationAsync` runs one `PeriodicWorker` pass and does not sleep. `RunBatchAsync` runs one queue batch. Those methods call a public seam on the workers (`IWorkerIterations`, `IQueueBatches`), not `InternalsVisibleTo`. They ignore `IsEnabled`, so a disabled worker still runs one pass. `OnStartingAsync` runs on the first call for that worker and not again. The hosted loop does honor `IsEnabled`: start `tester.Host` and a disabled worker logs once, does not iterate, and does not register a heartbeat.

`RunCommandAsync` invokes the command tree in-process and returns the exit code, stdout, and stderr. `help` and `version` do not build a host. Connector commands build one from the same catalog, including the in-memory overrides, and skip Windows Service and systemd registration.

`CliReference.AssertUpToDate` compares rendered `help --markdown` with a file. Line endings are normalized to `\n`. The exception tells you how to regenerate. Set `WORKERKIT_UPDATE_CLI_DOC=1` to rewrite the file instead.

```csharp
await using var tester = WorkerAppTester.Create<Program>(options =>
        options.ApplicationAssembly = typeof(Program).Assembly)
    .ConfigureApp(app => app.AddExampleConnector())
    .UseSetting("OpenMeteo:Enabled", "true")
    .UseSetting("OpenMeteo:Latitude", "52.52")
    .UseSetting("OpenMeteo:Longitude", "13.41")
    .Build();

var iteration = await tester.RunIterationAsync<ForecastWorker>();
var help = await tester.RunCommandAsync("help");
var markdown = await tester.RunCommandAsync("help", "--markdown");
CliReference.AssertUpToDate(
    "doc/cli.md",
    markdown.StandardOutput,
    "dotnet run --project src/ExampleHost --no-launch-profile -- help --markdown > doc/cli.md");
```

`TestCommandArgs` builds an `ICommandArgs` for a command exercised without the parser. `InMemoryQueue` reads pending count and dead letters from `InMemoryWorkQueue<T>`.

Repository: https://github.com/pkolasin/WorkerKit
