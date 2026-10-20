# Contoso.Billing

Instructions for agents changing this connector. The host is a WorkerKit app: one executable, layered configuration, and CLI commands on the same container. Package behavior is documented at https://github.com/pkolasin/WorkerKit/blob/main/llms.txt.

The host targets `__TARGET_FRAMEWORK__`. WorkerKit packages also support a `net9.0` project through their `net8.0` assets. This template only generates `net10.0` or `net8.0`.

## Layout

- `src/Contoso.Billing.Host/Program.cs` creates `WorkerApp`, adds commands, and calls `ConfigureHost`.
- `AddContosoBillingConnector` in `ContosoBillingConnectorExtensions` is the composition root. Register options, clients, workers, and handlers there.
- `SamplePollingWorker` is the periodic worker. `SampleCommand` is the CLI command. `SampleApiClient` is the typed HTTP client.
- `src/Contoso.Billing.Host/Queue/SampleQueue.cs` exists only when the connector was generated with `--queue`. It is an in-memory sample, not a durable broker.
- `tests/Contoso.Billing.Tests` uses `WorkerKit.Testing`. `doc/cli.md` is the checked-in `help --markdown` output.
- `Directory.Packages.props` pins `WorkerKit.Hosting` and `WorkerKit.Testing` to `WKVER`.

`Product` and the root namespace are `Contoso.Billing`. The assembly is `Contoso.Billing.Host`. The environment prefix is `CONTOSOBILLING`. Publish writes those names into `workerkit.app.json`.

## Build, test, publish

```bash
dotnet build
dotnet test
dotnet run --project src/Contoso.Billing.Host -- --help
dotnet run --project src/Contoso.Billing.Host --no-launch-profile -- help --markdown > doc/cli.md
dotnet publish src/Contoso.Billing.Host -c Release -o artifacts/publish
```

`--no-launch-profile` keeps the launch-settings banner out of `doc/cli.md`. Warnings are errors. Exit code `0` is success. Exit code `1` is a command failure, invalid configuration, or a usage error.

`-p:UseLocalWorkerKit=<path>` swaps the WorkerKit packages for `ProjectReference` against a checkout that contains `src/WorkerKit.Hosting`.

## Add a periodic worker

Follow `SamplePollingWorker`.

1. Subclass `PeriodicWorker` and take `WorkerContext` plus this worker's own singleton dependencies, such as `IOptionsMonitor<T>`.
2. Set `WorkerName` and `Interval`. Implement `RunOnceAsync` and return how many items the iteration processed. An empty result is still a success.
3. Resolve scoped services, including typed HTTP clients, from the `IServiceProvider` argument. A hosted service is a singleton, so do not inject scoped services into the worker.
4. `IsEnabled` defaults to true. `SamplePollingWorker` returns `Sample:Enabled`, which defaults to false, so `run` does not call the placeholder endpoint. A disabled worker logs once, does not iterate, and does not register a heartbeat.
5. Honor the cancellation token in delays and I/O. Delays use `WorkerContext.Time` (`TimeProvider`), not `Thread.Sleep`.
6. Register it from `AddContosoBillingConnector` with `AddHostedService`.

`RunIterationAsync` ignores `IsEnabled` and does not sleep. The hosted loop honors `IsEnabled`.

## Add a queue worker

`QueueWorker<T>` claims batches from `IWorkQueue<T>`. Complete runs only after the handler has stored its result. Release puts the item back on a transient error or on shutdown. Fail quarantines it. A lease that expires is claimed again, so delivery is at-least-once and the handler must be idempotent. Items that share a `PartitionKey` are not leased together. Order is not guaranteed.

When `Queue/SampleQueue.cs` is present, copy `SampleQueueWorker`. It is registered with `AddInMemoryQueueWorker` under the name `sample-queue`. `Queue:sample-queue:Enabled` defaults to false. `InMemoryWorkQueue<T>` is not durable.

For a real queue, register the broker first, then the worker:

```csharp
services.AddSingleton<IWorkQueue<MyPayload>, MyQueue>();
services.AddQueueWorker<MyQueueWorker, MyPayload, MyHandler>(configuration, "my-queue");
```

The worker constructor takes `WorkerContext`, the queue, and `IOptionsMonitor<QueueWorkerOptions>`. Options bind from `Queue:{name}`. Throw `PermanentWorkItemException` for a poison message. Anything else is a retry.

## Add a CLI command

Commands and the host share one configuration stack and one DI container. The command tree is built from the static `Spec` before the host exists, so `help` and `doc/cli.md` include the command without constructing it.

1. Implement `IWorkerCommand`. Declare each option as a static `CommandOption<T>` and expose `static CommandSpec Spec`. `ExecuteAsync` receives `ICommandArgs`.
2. Supported values are `string`, `bool` (a flag), `int`, `long`, `double`, `decimal`, `TimeSpan`, `DateOnly`, `DateTimeOffset`, `FileInfo`, `DirectoryInfo`, and arrays of those. Enums use `EnumCommandOption<TEnum>` or `EnumCommandArgument<TEnum>`. A flags enum accepts one named member. Any other `T` fails when the command is registered.
3. Do not discover options with reflection. The spec is the whole contract, which keeps the command tree AOT-clean.
4. Register it in `Program.cs` with `app.Commands.Add<MyCommand>()` or `app.Commands.AddGroup(...)`.
5. A root name or alias of `run`, `check-config`, `version`, or `help` fails at startup. `run` is also the default when no command is given.
6. Leave static `ValidateOptions` true unless the command reports configuration errors itself. Set static `EnableTelemetry` to record traces on every invocation. Otherwise the caller can pass `--telemetry`.
7. Update `doc/cli.md` in the same change. The test below fails when the file is stale.

`SampleCommand` is the copyable command. It delegates to `SampleHandler`.

## Configuration and instances

`Sample:Enabled` defaults to false. `Sample:ItemId` empty makes `SamplePollingWorker` log and skip HTTP. `SampleApi:BaseAddress` is `https://example.invalid/api/` until you replace it. Register a real client with `AddResilientJsonClient`. Do not call `new HttpClient()`.

Later sources win: install `appsettings.json`, environment-specific JSON, instance JSON when the instance is not `default`, user secrets in Development, environment variables, then the command line. `run` and `check-config` also accept `--Section:Key value`. The content root is the install directory, not the process working directory.

Instance selection, before the host is built:

1. `--instance-dir`, otherwise `CONTOSOBILLING_INSTANCE_DIR`
2. otherwise `--instance` or `CONTOSOBILLING_INSTANCE`, under `CONTOSOBILLING_INSTANCES_ROOT` (default `%ProgramData%\Contoso.Billing\instances` or `/var/lib/contoso-billing`)
3. otherwise instance `default`, whose directory is the install directory

The instance name matches `^[a-z0-9][a-z0-9-]{0,39}$`. `run` takes an exclusive lock on `{instance}/.lock`. `check-config`, `version`, and connector commands do not lock. `check-config` prints the resolved directories.

## Tests

`WorkerAppTester` uses the same `ConfigureHost` delegates, an in-memory configuration override, and a temporary instance directory. It does not start the hosted loop and does not export OpenTelemetry unless you set `EnableTelemetry` and an OTLP endpoint.

```csharp
await using var tester = WorkerAppTester.Create<SamplePollingWorker>(options => options.EnvPrefix = "CONTOSOBILLING")
    .ConfigureApp(app => app.ConfigureHost(builder => builder.AddContosoBillingConnector()))
    .UseSetting("Sample:ItemId", "demo")
    .Build();

var iteration = await tester.RunIterationAsync<SamplePollingWorker>();
var command = await tester.RunCommandAsync("sample", "--id", "demo");
```

`RunIterationAsync` is one periodic pass. `RunBatchAsync` is one queue batch. Both ignore `IsEnabled`. `RunCommandAsync` returns the exit code and captured output. `help` and `version` do not build a host.

`TestCommandArgs.With` builds `ICommandArgs` when a test calls `ExecuteAsync` without the parser, which is what `SampleCommandTests` does.

`CliReference.AssertUpToDate` compares rendered markdown with `doc/cli.md`. Line endings are normalized. The existing test is `Cli_reference_is_up_to_date`. Set `WORKERKIT_UPDATE_CLI_DOC=1` to rewrite the file instead of failing. The checked-in regenerate command is:

```bash
dotnet run --project src/Contoso.Billing.Host --no-launch-profile -- help --markdown > doc/cli.md
```

## Deploy

`dotnet publish` writes `deploy/` and `workerkit.app.json` next to the app. The scripts read the service name, the environment prefix `CONTOSOBILLING`, and the install paths from that file. `-p:WorkerKitDeployScripts=false` skips both. Install, update, and uninstall are documented at https://github.com/pkolasin/WorkerKit/blob/main/doc/deploy.md.

The Windows service is `Contoso.Billing` for instance `default` and `Contoso.Billing-<name>` otherwise. Linux units are `Type=notify`. Docker, when the Dockerfile is present, uses `CONTOSOBILLING_INSTANCE_DIR=/instance`.

## Rules

- Keep command options on the static `CommandSpec`. No reflection over command properties.
- Do not reuse the root names `run`, `check-config`, `version`, or `help`.
- Update `doc/cli.md` when the command tree changes, and keep `CliReference.AssertUpToDate` passing.
- Do not inject scoped services into a `PeriodicWorker` or `QueueWorker`.
- Leave `Sample:Enabled` and queue `Enabled` flags false until the real call should run.
- Telemetry export stays off unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. `run` always records. A connector command records when `EnableTelemetry` is true or the invocation passes `--telemetry`.
- Do not clear NuGet caches, and do not add a `PackageReference` that replaces WorkerKit.Hosting with a different host stack.
