# WorkerKit

Repository: https://github.com/pkolasin/WorkerKit

Generic host for data connectors and processing workers. One program runs as a console app, in Docker, as a Windows Service, and as a systemd service (`Type=notify`). It includes DI, layered configuration, structured file logging (Serilog), `IHttpClientFactory` with resilience (retry, timeout, circuit breaker), and CLI commands over the same container. The samples under `samples/` are not part of the packages: an EU VIES SOAP VAT check, an Open-Meteo REST worker, a VIES queue worker, and an outbound MQTT and SMS queue. Copy the host with `dotnet new`, then replace those samples.

## Layout

```
src/WorkerKit.Abstractions   contracts (queues, identity, options). Packs as WorkerKit.Abstractions.
src/WorkerKit.Hosting        host, CLI, health, Serilog, OpenTelemetry, workers. Packs as WorkerKit.Hosting.
src/WorkerKit.Soap           generic WCF binding helpers. Packs as WorkerKit.Soap.
src/WorkerKit.Testing        in-process host, one-iteration workers, and CLI capture for tests. Packs as WorkerKit.Testing.
src/WorkerKit.Templates      `dotnet new workerkit-connector`. Packs as WorkerKit.Templates.
src/ExampleHost              sample composition root. Assembly ExampleHost, product WorkerKitExample.
samples/Vies                 EU VIES SOAP client, `check-vat`, and the `vies-queue` worker
samples/OpenMeteo            Open-Meteo REST client and the `open-meteo` periodic worker
samples/Outbound             MQTT publish (Mosquitto or Azure IoT Hub) and SMS (dry-run or Twilio), plus `outbound-queue`
tests/WorkerKit.Hosting.Tests   library tests, net8.0 and net10.0
tests/WorkerKit.Templates.Tests packs the libraries, instantiates the template, and builds it
tests/ExampleHost.Tests          host composition tests, net10.0
tests/WorkerKit.Samples.Tests    sample tests, net10.0
doc/deploy.md                how to publish, install, update, and uninstall a connector
```

There is no `WorkerKit.Data` package. `Data:Provider` `Dapper` is the hook for a connector that registers `IDbConnectionFactory`. SQL Server and PostgreSQL packages come later. Publish writes `deploy/` from WorkerKit.Hosting; this repo does not keep a `deploy/` tree of its own.

`Program.cs` creates a `WorkerApp` and calls `AddExampleConnector`. That extension is the sample composition root: `check-vat` plus the Open-Meteo, VIES queue, and outbound queue workers. `WorkerHostBuilder` does not reference the samples. A connector passes its own `ConfigureHost` delegates.

Library namespaces match the folder under `WorkerKit.*`. The host root namespace is `ExampleHost`. Sample namespaces are `WorkerKit.Samples.Vies`, `WorkerKit.Samples.OpenMeteo`, and `WorkerKit.Samples.Outbound`. The executable is `ExampleHost`. The product and Windows service name are `WorkerKitExample`, and the environment prefix is `WORKERKITEXAMPLE`. None of the sample projects are packed.

## Prerequisites

- .NET SDK 10 (`global.json` rolls forward within the 10.x feature band)
- .NET 8 targeting pack, because the WorkerKit packages multi-target `net10.0` and `net8.0`. A project that targets `net9.0` is supported through the `net8.0` assets. There is no separate `net9.0` target.
- For the container image: Docker and the `mcr.microsoft.com/dotnet/sdk:10.0` / `runtime:10.0` images
- For the VIES regeneration script only: `dotnet-svcutil` 8.0.0, which runs on the .NET 9 runtime. The build does not need it.

## Packages and versions

The packages are WorkerKit.Abstractions, WorkerKit.Hosting, WorkerKit.Soap, WorkerKit.Testing, and WorkerKit.Templates. `src/ExampleHost` and `samples/` are not packed.

`dotnet pack` on a workstation, and the CI workflow, produce `0.1.1-dev.<build>`. Locally `<build>` is `local` unless `WorkerKitBuildNumber` is set. On GitHub Actions it is the workflow run number. The assembly informational version of those packs stays `0.1.1`.

A version with no `-dev` suffix is packed only by [`.github/workflows/release.yml`](.github/workflows/release.yml), from the tag of a published GitHub Release. The first release tag is `v0.1.0-preview.1`, which packs `0.1.0-preview.1` and publishes the five packages and their symbol packages to nuget.org.

## Create a connector

Pack the template (that package version becomes the default `--workerkit-version`), install that nupkg, and generate a connector. The example host in this repository is not the template.

```bash
dotnet pack src/WorkerKit.Templates/WorkerKit.Templates.csproj -c Release -o artifacts/packages
dotnet new install artifacts/packages/WorkerKit.Templates.*.nupkg
dotnet new workerkit-connector -n Contoso.Billing -o ../Contoso.Billing
```

`-n` is the C# namespace and the product name (`Contoso.Billing` -> assembly `Contoso.Billing.Host`, environment prefix `CONTOSOBILLING`). It must match `^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$`. `--env-prefix`, `--framework` (`net10.0` or `net8.0`), `--workerkit-version`, `--queue`, and `--docker` are optional. A new user-secrets id is generated. Uninstall with `dotnet new uninstall WorkerKit.Templates`.

To compile the connector against this checkout instead of the packages, build it with `-p:UseLocalWorkerKit=<path-to-this-repo>`. That swaps `PackageReference` for `ProjectReference`, so the cached-package guard in this repo still applies only to NuGet restores.

Run the example in place:

```bash
dotnet build
dotnet test
dotnet run --project src/ExampleHost -- --help
dotnet run --project src/ExampleHost -- help
dotnet run --project src/ExampleHost -- help check-vat
dotnet run --project src/ExampleHost -- version
dotnet run --project src/ExampleHost -- check-config
dotnet run --project src/ExampleHost -- run
```

`help` lists every command, option, alias, argument, default, and required flag by walking the command tree, then a short section on host configuration overrides (`--Section:Key`, `Section__Key`) and instance options. `help check-vat` prints the same text as `check-vat --help`. `help` does not build the host, so it still runs when configuration is invalid. The checked-in reference is [doc/cli.md](doc/cli.md). Regenerate it with:

```bash
dotnet run --project src/ExampleHost --no-launch-profile -- help --markdown > doc/cli.md
```

`--no-launch-profile` keeps the launch-settings banner out of the file. `dotnet run` with no command also starts the host. `Ctrl+C` and `SIGTERM` stop it. Exit code `0` is success, `1` is a command failure, invalid configuration, or a usage error.

## Run modes

The same build calls `AddWindowsService` and `AddSystemd`. Each integration turns itself on only when the process is actually hosted that way.

### Console

`dotnet run --project src/ExampleHost` uses `Properties/launchSettings.json`, which sets `DOTNET_ENVIRONMENT=Development`. The Open-Meteo worker logs a heartbeat and does not call the network unless both `OpenMeteo:Latitude` and `OpenMeteo:Longitude` are set. The VIES and outbound queues stay off until their `Enabled` flags are true. With no instance selected, the instance name is `default` and its directory is the built assembly's directory, so Serilog writes `bin/Debug/net10.0/logs/worker-YYYYMMDD.json`.

### Docker

```bash
docker build -t workerkit-example .
docker run --rm workerkit-example version
docker run --rm -e OpenMeteo__PollIntervalSeconds=30 workerkit-example run
```

The image is multi-stage, runs as the runtime image's built-in user `app` (`USER $APP_UID`, uid 1654), and uses `DOTNET_ENVIRONMENT=Production`. `WORKERKITEXAMPLE_INSTANCE_DIR=/instance` and `VOLUME /instance` put configuration, logs, and data on a volume owned by that user. `/app` is the install directory (the image) and stays the content root. The sample health listener uses an unprivileged port (`http://127.0.0.1:5088/health/`), which that user can bind.

```bash
docker run --rm -v worker-instance:/instance workerkit-example run
docker run --rm -v D:\ws\instance:/instance -e WORKERKITEXAMPLE_INSTANCE=east workerkit-example run
```

A named volume inherits the image ownership (uid 1654). A bind mount keeps the host directory's ownership, so that directory has to be writable by uid 1654. One container is one instance. The `run` lock also stops two containers from sharing a volume on a local disk; `flock` is not reliable on SMB or NFS. `docker run workerkit-example check-config` overrides the image `CMD` and prints the instance directory.

### Windows Service and systemd

`dotnet publish` writes `workerkit.app.json` and `deploy/` next to the app. The scripts read every name from that file. For this example the service is `WorkerKitExample`, the Windows install path is `C:\Program Files\WorkerKitExample`, and the Linux install path is `/opt/worker-kit-example`. Install, update, uninstall, `-HealthListenUrl`, and the virtual-account URL reservation are in [doc/deploy.md](doc/deploy.md). `-p:WorkerKitDeployScripts=false` skips the copy.

`AddWindowsService` registers the SCM lifetime when the process is started by the Service Control Manager. The binary path includes `run`. With no command the executable also starts the host. The Windows service name is `WorkerKitExample` for instance `default` and `WorkerKitExample-<name>` otherwise.

`AddSystemd` sends `READY=1` when systemd sets `INVOCATION_ID`, so the generated units are `Type=notify`. Framework-dependent publish expects the .NET 10 runtime on the machine. For a machine without the runtime, publish with `-r linux-x64 --self-contained true` (or `-r win-x64` on Windows).

## Configuration

`Host.CreateApplicationBuilder` loads, in order (later wins):

1. `{install}/appsettings.json`
2. `{install}/appsettings.{Environment}.json` (`appsettings.Development.json` is not published)
3. `{instance}/appsettings.json` when the instance is not `default`
4. `{instance}/appsettings.{Environment}.json`
5. User secrets when the environment is Development (`dotnet user-secrets` on the entry assembly; for this host that is the Host project). The application name is the product identity (`WorkerKitExample` here), which is not the Host assembly name, so `Assembly.Load(ApplicationName)` never sees `UserSecretsId`. Development adds secrets from the entry assembly after the JSON files and before environment variables and the command line. A secret overrides `appsettings`; an environment variable or command-line argument overrides the secret.
6. Environment variables (`Worker__PollIntervalSeconds=30`)
7. Command line

`DOTNET_ENVIRONMENT` is host configuration. An instance `appsettings.json` cannot set it. Set it on the unit, the service, or with `--environment`. Optional `{ApplicationName}.settings.json` files, if you add them, are still read after the instance files.

`run` and `check-config` accept extra `--Section:Key value` tokens and pass the original arguments to the host, so this works:

```bash
dotnet run --project src/ExampleHost -- run --Worker:PollIntervalSeconds 12
dotnet run --project src/ExampleHost -- check-config --Worker:PollIntervalSeconds 0
```

The root command stays strict, so an unknown command is an error. `--instance-dir` and `--instance` are recursive global options, so every command accepts them. Options classes use `IOptions<T>` with `ValidateDataAnnotations` and `ValidateOnStart`. Cross-field HTTP rules (total timeout greater than the attempt timeout, circuit-breaker sampling at least twice the attempt timeout) are checked by `IValidateOptions`. Invalid options fail `run` and `check-config` before the worker loop starts.

The content root is always the install directory (`AppContext.BaseDirectory`, or the `contentRoot` argument of `WorkerHostBuilder.Create` in tests). It does not follow the process working directory, so a Windows service that starts in `System32` still reads the install directory's `appsettings.json`.

## Instances

One install directory can run several processes. The instance directory holds that process's `appsettings.json`, logs, data, and temp files. Names come from the application identity (`WorkerAppIdentity`), derived from the entry assembly: product name, service name, environment prefix, version, and the default instance roots. This host's product is `WorkerKitExample`, so the prefix is `WORKERKITEXAMPLE`, the Windows root is `%ProgramData%\WorkerKitExample\instances`, the Linux root is `/var/lib/worker-kit-example`, and the service is `WorkerKitExample` (named instances `WorkerKitExample-<name>`). An explicit override replaces the name, service name, or prefix. Publish writes the same values into `workerkit.app.json`, and the deploy scripts read that file. Resolution happens before the host is built:

1. `--instance-dir`, otherwise `WORKERKITEXAMPLE_INSTANCE_DIR`
2. otherwise `--instance` or `WORKERKITEXAMPLE_INSTANCE`, under `WORKERKITEXAMPLE_INSTANCES_ROOT` (default `%ProgramData%\WorkerKitExample\instances` or `/var/lib/worker-kit-example`)
3. otherwise instance `default`, whose directory is the install directory

The instance name is `--instance` or `WORKERKITEXAMPLE_INSTANCE`, otherwise the directory name, otherwise `default`. It must match `^[a-z0-9][a-z0-9-]{0,39}$`. Relative `LogFile:Path`, `Instance:DataDirectory`, and `Instance:TempDirectory` resolve against the instance directory. Absolute paths stay absolute. `Instance:CleanTempOnStart` (default true) deletes children of the temp directory on startup and refuses when that path is, or contains, the instance directory, the install directory, or the data directory.

`check-config` prints the instance name, both directories, and the resolved log and data paths. `run` takes an exclusive lock on `{instance}/.lock` (`FileShare.None`, which is `flock(LOCK_EX)` on Linux) and writes `{instance}/instance.pid`. A clean shutdown deletes that pid file before releasing the lock, and only when the file still contains this process's id. A stale `instance.pid` whose process is gone, whose executable is not this install, or which was started with `--instance-dir`, does not block `update`. Connector commands, `check-config`, and `version` do not lock. `Instance:RequireLock` set to false skips the lock. `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` disables .NET's file locks, including this one, so leave it unset in production. The lock is not reliable on SMB or NFS.

`appsettings.instance.template.json` is published next to the host. The Windows install script copies it to a new instance directory when `appsettings.json` is missing. Instance secrets belong in that file, protected by the service account's ACL. A vault or `LoadCredential=` can be added later as another configuration source.

## Logging

Serilog is configured in the host and bridged to `ILogger<T>`. Application projects do not reference Serilog.

Serilog is the file logger because the sinks, levels, and templates live in `appsettings.json`, rolling files are built in, and compact JSON keeps the properties that structured logging already captured. Console output is the Serilog console sink from configuration. The rolling JSON file is attached in code from `LogFile:Path` so a relative path is rooted at the instance directory rather than at the content root or the working directory. Each event has an `Instance` property (and the console template prints it). Levels come from `Serilog:MinimumLevel`, including `Override` entries. When `run` has `OTEL_EXPORTER_OTLP_ENDPOINT` set, the same pipeline also writes an OTLP log sink. The sink copies `TraceId` and `SpanId` from the current activity and uses the same resource attributes as traces and metrics.

## Application builder

`WorkerApp.CreateBuilder<TProgram>(args)` records the identity, the command catalog, and `ConfigureHost` callbacks. It does not load configuration, create an instance directory, or build a host. `TProgram`'s assembly owns user secrets and `service.version`. A test host falls back to `Assembly.GetEntryAssembly`, and tests pass the program type explicitly. `options` can override `Name`, `ServiceName`, `EnvPrefix`, and `ApplicationAssembly`.

`RunAsync` builds the root command, parses `args`, and builds a host only for the command that was selected:

```csharp
var app = WorkerApp.CreateBuilder<Program>(args, options =>
{
    options.Name = "Contoso";
    options.EnvPrefix = "CONTOSO";
});
app.Commands.Add<FetchCustomersCommand>();
app.ConfigureHost(builder => builder.AddContosoConnector());
return await app.RunAsync();
```

This repository's `Program.cs` is the same shape and calls `AddExampleConnector`. `ConfigureHost` callbacks run in registration order after the instance, Windows/systemd integration, Serilog, and health checks, and before telemetry. `builder.GetInstance()` returns the `IInstanceContext`, including from a test that uses `WorkerHostBuilder`. `run` always records telemetry. A connector command records it when `EnableTelemetry` is true or the invocation passes `--telemetry`. `help` and `version` do not build a host. `check-config` accepts `--telemetry` and ignores it.

`services.AddValidatedOptions<T>(section)` binds and validates an options section. `services.AddResilientJsonClient<TClient, TImplementation, TOptions>(section)` registers a typed HTTP client with the standard resilience handler. `services.AddQueueWorker<TWorker, TPayload, THandler>(configuration, name)` registers named queue options, a scoped handler, and the hosted worker. Register `IWorkQueue<T>` first, or call `AddInMemoryQueueWorker` for the sample queue. `builder.AddTelemetrySource(name)` adds an `ActivitySource` and meter. Telemetry subscribes to that name, and exports it when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

## Add a worker

1. Subclass `PeriodicWorker` and take one `WorkerContext` plus the worker's own dependencies. Set `WorkerName` and `Interval`. Implement `RunOnceAsync` and return how many items the iteration processed. An empty result is still a success.

```csharp
public sealed class MyConnectorWorker(WorkerContext context, IOptionsMonitor<MyOptions> options)
    : PeriodicWorker(context)
```
2. The base class registers a heartbeat, opens a scope for each iteration, and logs a failure without stopping the host. Take scoped services, including typed HTTP clients, from the `IServiceProvider` argument. Do not inject them into the worker; a hosted service is a singleton.
3. Honor `stoppingToken` in delays and I/O so shutdown finishes inside `HostOptions:ShutdownTimeout` (15 seconds in `appsettings.json`). An exception that escapes `ExecuteAsync` still stops the host (`BackgroundServiceExceptionBehavior` stays at the default).
4. Register it from `ConfigureHost`:

```csharp
app.ConfigureHost(builder => builder.Services.AddHostedService<MyConnectorWorker>());
```

`ForecastWorker` (`open-meteo`) is the copyable periodic sample. `PeriodicWorker.IsEnabled` defaults to true. The sample returns `OpenMeteo:Enabled` (default true). `QueueWorker` returns `QueueWorkerOptions.Enabled`. The hosted loop logs once when that is false, does not iterate, and does not register a heartbeat, so readiness is unchanged. `RunIterationAsync` and `RunBatchAsync` ignore the switch. Set `OpenMeteo:Enabled` to `false` or delete the registration when you replace the sample. With `OpenMeteo:Latitude` or `OpenMeteo:Longitude` unset, the iteration logs and does not call the network. When both are set it requests `current=temperature_2m` and counts one processed item. `IWorkerIterationObserver` records the iteration span and the `worker.iterations`, `worker.iteration.duration`, and `worker.items.processed` metrics. `run` registers that observer, and so does a connector command that enables telemetry. The other commands keep the no-op. `check-vat` sets `EnableTelemetry`, so a direct lookup records traces when it runs.

## Add a queue worker

A queue connector subclasses `QueueWorker<T>` instead of writing its own loop. `PeriodicWorker` and `ForecastWorker` stay as they are. Core only defines the contract: `IWorkQueue<T>`, `WorkItem<T>`, `IWorkItemHandler<T>`, and `PermanentWorkItemException`. The skeleton does not reference RabbitMQ, Azure Service Bus, or any other broker. The outbound sample uses MQTTnet only inside `samples/Outbound`, which is not packed.

Claim leases a batch. Complete runs only after the handler has stored its result. Release puts the item back, with an optional delay, on a transient error or on shutdown. Fail quarantines it. A lease that expires is claimed again, so delivery is at-least-once and the handler must be idempotent. Order is not guaranteed. Items that share a `PartitionKey` are not leased at the same time, which is what makes `MaxConcurrency` greater than 1 safe for that key.

```csharp
services.AddSingleton<IWorkQueue<MyPayload>, MyQueue>();
services.AddQueueWorker<MyQueueWorker, MyPayload, MyHandler>(configuration, "my-queue");
```

`AddQueueWorker` registers named options from `Queue:{worker name}`, a scoped handler, and the hosted worker. The worker constructor takes `WorkerContext`, the queue, and `IOptionsMonitor<QueueWorkerOptions>`. `AddInMemoryQueueWorker` does the same and registers `InMemoryWorkQueue<T>` (example only, not durable). Replace `IWorkQueue<T>` with a database or broker implementation in a separate project when you leave the sample. `InMemoryWorkQueue<T>` keeps quarantined items in `DeadLetters` and logs the reason through the worker.

`Queue:vies-queue:Enabled` and `Queue:outbound-queue:Enabled` default to false, so `run` does not poll either sample queue. Set `vies-queue` to true and set `CountryCode` plus `VatNumber` to enqueue one VIES lookup at start (`ViesQueueWorker`). Set `outbound-queue` to true and set `ExampleKind` (`mqtt` or `sms`), `ExampleDestination`, and `ExampleBody` to enqueue one outbound item (`OutboundQueueWorker`). An empty queue is still a successful heartbeat. A failed claim fails the iteration and shows up in readiness. Empty polls back off from `MinIdleDelay` to `MaxIdleDelay`. `MaxIdleDelay` has to stay inside the heartbeat grace period computed from `MinIdleDelay`, or an idle queue looks stuck.

Shutdown stops new claims, waits for handlers that already started, and releases anything still leased so those items return to the queue. Handlers should honor the cancellation token.

On `run`, each item is a child span of `{worker} iteration`, named `{worker} item`, with tags `work.item.id`, `work.item.attempt`, and `work.item.outcome`. A handler failure sets the span status to error. `worker.items.processed` counts those items with `outcome` of `done`, `retry`, or `failed`. `worker.queue.pending` is the number of items waiting to be claimed. `check-config` prints the remembered queue keys, including `Queue:vies-queue:Enabled` and `Queue:outbound-queue:Enabled`. A VIES lookup from the queue is a child span named `Vies checkVat`. An unknown outbound kind is a permanent failure and is dead-lettered.

## Add a CLI command

Commands and the host share one configuration stack and one DI container. The command tree is built from the static `Spec` before the host, so `help` and [doc/cli.md](doc/cli.md) include connector commands without constructing the command or starting the host. `help` and `version` do not build the host. `run` always records telemetry. A connector command records it when `EnableTelemetry` is true or the invocation passes `--telemetry`. Export stays off unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. `check-config` accepts `--telemetry` and ignores it. A short command flushes traces, metrics, and logs before it returns.

1. Implement `IWorkerCommand` from `WorkerKit.Abstractions`. Declare each option as a static `CommandOption<T>` and expose `static CommandSpec Spec`. The host reads `Spec` without calling the constructor, so `help` works before any dependency exists. `ExecuteAsync` receives `ICommandArgs`. The command itself is constructed from DI in a scope, so dependencies are constructor parameters. Leave the static `ValidateOptions` true unless the command reports configuration errors itself. Set the static `EnableTelemetry` to record traces and metrics on every invocation. Supported values are `string`, `bool` (a flag), `int`, `long`, `double`, `decimal`, `TimeSpan`, `DateOnly`, `DateTimeOffset`, `FileInfo`, `DirectoryInfo`, and arrays of those. Enums use `new EnumCommandOption<TEnum>` or `new EnumCommandArgument<TEnum>`, and enum arrays use `EnumArrayCommandOption<TEnum>` or `EnumArrayCommandArgument<TEnum>`. A flags enum accepts one named member; combined values such as `Read, Write` are rejected. Any other `T` fails when the command is registered, before the command runs.
2. Register it on the builder: `app.Commands.Add<MyCommand>()` or `app.Commands.AddGroup("group", "Description.", group => group.Add<MyCommand>())`. A root name that matches `run`, `check-config`, `version`, or `help` fails at startup. This host adds the example from `AddExampleConnector`.
3. `help` and [doc/cli.md](doc/cli.md) pick the command up from the tree. Regenerate the markdown when the test says it is stale.

```csharp
public sealed class LookupCommand(IMyClient client) : IWorkerCommand
{
    static readonly CommandOption<string> Id = new("--id", "Record id to look up.") { Required = true };

    public static CommandSpec Spec { get; } = new("lookup", "Look up one record.") { Id };

    public async Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
        => await client.GetAsync(args.Get(Id), cancellationToken) is null ? 1 : 0;
}
```

`check-vat` is the sample. Exit 0 means VIES reported the number as valid. Exit 1 means the number is not valid, or the arguments failed to parse.

```bash
dotnet run --project src/ExampleHost -- check-vat --country-code DE --vat-number 123456789
```

## Add a REST client

1. Add an interface and options that extend `HttpClientEndpointOptions` in the connector (framework options live in `WorkerKit.Abstractions`).
2. Implement the client with the `HttpClient` constructor parameter only. Do not call `new HttpClient()`.
3. Register it:

```csharp
services.AddResilientJsonClient<IMyClient, MyClient, MyClientOptions>(
    configuration.GetSection(MyClientOptions.SectionName));
```

`AddResilientJsonClient` binds the options section, validates it on start, and attaches `Microsoft.Extensions.Http.Resilience`'s standard handler. Retry uses exponential backoff with jitter. Per-attempt timeout, total timeout, and the circuit breaker come from the `Resilience` object on that client, so two clients can differ. `HttpClient.Timeout` is `InfiniteTimeSpan` because the resilience handler owns both timeouts; the default 100 second client timeout would race them.

The Open-Meteo client (`GET https://api.open-meteo.com/v1/forecast?latitude=&longitude=&current=temperature_2m`) is the sample. It needs no API key. Latitude and longitude are optional; the worker skips the call when either is missing.

## Add a SOAP client

The checked-in proxy in `samples/Vies/Generated/CheckVatService.cs` was generated from `samples/Vies/wsdl/checkVatService.wsdl` (the European Commission VIES `checkVatService` WSDL). The project does not download the WSDL and does not need the service to build.

Regenerate after you refresh the WSDL file:

```bash
dotnet tool install --global dotnet-svcutil --version 8.0.0
./samples/Vies/regenerate-vies.sh
```

`dotnet-svcutil` 8.0.0 runs on the .NET 9 runtime. The script drops the net6-era `CloseAsync` block because `ClientBase<T>` already provides it.

`ViesVatChecker` is registered as a singleton and constructs a `checkVatPortTypeClient` per call, then `CloseAsync` or `Abort` through `SoapChannel.AbortUnlessCompleted`. Do not cache `ClientBase<T>`: a faulted channel cannot be reused. Endpoint and timeouts come from `Vies`. The sample endpoint is `https://ec.europa.eu/taxation_customs/vies/services/checkVatService`. The checked-in WSDL `soap:address` is still `http://`, and WCF does not follow the redirect to HTTPS, so the options point at the HTTPS URL. The country code is two letters. The VAT number is 2 to 12 characters; a repeated country prefix and spaces are removed before the call. `check-vat` calls the service once. The call is a span named `Vies checkVat` and records `vies.client.duration` on the `WorkerKit.Samples.Vies` meter. A failed call sets the span status to error.

`System.ServiceModel.Http` is the supported way to call an existing document/literal SOAP service on modern .NET. The generated contract stays in the sample. `WorkerKit.Soap` only builds the binding and aborts a channel that did not close.

## Add a database

The host does not ship a database project. `Data:Provider` `None` skips the probe. Set it to `Dapper` and register `IDbConnectionFactory` from `ConfigureHost` when a connector needs one. `check-config` masks the connection string. If the provider is `Dapper` and no factory is registered, the readiness check `data-store` is Unhealthy. A registered factory makes `/ready` run `SELECT 1`.

## Outbound MQTT and SMS

`samples/Outbound` is an in-memory queue of `{ kind, destination, body }`. It is not a package.

MQTT defaults to a local Mosquitto broker (`localhost:1883`, no TLS, topic `workerkit/samples/outbound`, client id `workerkit-sample`). Set `Mqtt:Provider` to `AzureIoTHub` and supply `Host` (the hub hostname), `Port` `8883`, `DeviceId`, and `DeviceSecret` (the device shared access key, base64). The sample builds the username `{host}/{deviceId}/?api-version=2021-04-12`, the SAS password, and the topic `devices/{deviceId}/messages/events/`. It does not reference the Azure IoT SDK. `Mqtt:Password` and `Mqtt:DeviceSecret` are masked by `check-config`.

SMS uses `ISmsSender`. `Sms:Provider` defaults to `DryRun`, which logs the message. `Twilio` posts `application/x-www-form-urlencoded` `To`, `From`, and `Body` to `https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json` with HTTP basic auth. `Sms:AuthToken` is masked by `check-config`. No Twilio SDK is referenced.

## Health

`AddHealthChecks` registers four checks. `LoggingHealthCheckPublisher` still writes the report on `Health:PublisherPeriodSeconds`.

| Check | Tag | Meaning |
|---|---|---|
| `self` | `live` | The process is running. |
| `workers` | `live`, `ready` | Heartbeats from each `PeriodicWorker`. |
| `instance-dirs` | `ready` | `data/` and `temp/` exist and accept a write. |
| `data-store` | `ready` | `SELECT 1` when `Data:Provider` is not `None`. `None` stays Healthy and does not open a connection. |

A worker's grace period is the larger of `interval * Health:HeartbeatStaleMultiplier` (default 3) and `interval + Health:HeartbeatGracePadding` (default 5 minutes). Before the first success, the worker stays Healthy for that grace period. After a success, further failures inside the grace window are Degraded. A success older than the grace period is Unhealthy, which is the stuck-loop signal. External APIs such as Open-Meteo and VIES are not their own liveness check: a failed iteration is only this heartbeat.

Set `Health:ListenUrl` to an `http://` prefix that ends with `/` and listens on `127.0.0.1`, `localhost`, or `::1` (for example `http://127.0.0.1:5088/health/`). Leave it empty and the process does not bind a port. The listener answers:

- `{prefix}` every check
- `{prefix}live` liveness (`self` and heartbeats)
- `{prefix}ready` readiness (heartbeats, directories, and the data store)

The body is JSON: `status`, `instance`, `version`, and `entries` (`status`, `description`, `data`, `duration`). Liveness and the combined report return `200` for Healthy and Degraded, and `503` for Unhealthy, so a Degraded iteration does not look like a dead process. Readiness returns `503` for Degraded and Unhealthy. `check-config` prints the three URLs and the grace settings.

On Windows the listener uses HTTP.sys. The URL namespace is closed until an administrator reserves a prefix, and that rule includes `http://127.0.0.1:port/` ([Namespace reservations](https://learn.microsoft.com/en-us/windows/win32/http/namespace-reservations-registrations-and-routing)). `install-service.ps1 -HealthListenUrl` stores the normalized prefix in that `Health:ListenUrl` for every account, including LocalSystem. The script adds the HTTP.sys reservation only when `-ServiceAccount` is an account other than LocalSystem, using that prefix. A virtual account is `NT SERVICE\<ServiceName>`. The commands are in [doc/deploy.md](doc/deploy.md). Linux and Docker do not use HTTP.sys.

`LoggingHealthCheckPublisher` still logs each report. `MetricsHealthCheckPublisher` stores it for the `worker.health.status` gauge (0 unhealthy, 1 degraded, 2 healthy) so the same result is visible through OTLP without opening the listen port.

## OpenTelemetry

`run` always registers telemetry. A connector command registers the same pipeline when `EnableTelemetry` is true or the invocation passes `--telemetry`. `check-config` accepts `--telemetry` and ignores it. `version` and `help` do not build a host. Export stays off unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

The setup follows Aspire ServiceDefaults without ASP.NET Core and without service discovery: one `AddWorkerDefaults` call, an OTLP exporter, and no Prometheus scrape endpoint. Export stays off until `OTEL_EXPORTER_OTLP_ENDPOINT` is set. `OTEL_EXPORTER_OTLP_PROTOCOL` is `grpc` (the default, port 4317) or `http/protobuf` (port 4318). `OTEL_EXPORTER_OTLP_HEADERS` is `key=value` pairs separated by commas. Log export uses the `OTEL_EXPORTER_OTLP_LOGS_*` keys when they are set. `Telemetry:ServiceName` overrides the service name (default `WorkerKitExample`).

Resource attributes on traces, metrics, and logs:

| Attribute | Value |
|---|---|
| `service.name` | `Telemetry:ServiceName`, or the application identity name (`WorkerKitExample` here) |
| `service.version` | informational version of the entry assembly, with a `+` suffix removed |
| `workerkit.version` | informational version of the WorkerKit.Hosting assembly |
| `service.instance.id` | instance name (`default` for a single install) |
| `host.name` | machine name |
| `deployment.environment.name` | host environment |

Traces cover `HttpClient` (Open-Meteo and, through `HttpClient`, the SOAP transport), each `PeriodicWorker` iteration (`{worker} iteration`), a SOAP span named `Vies checkVat`, and one span per queue item (`{worker} item`, a child of that iteration). `check-vat` and a VIES queue item place that SOAP span under the current activity. The library `ActivitySource` and `Meter` are named `WorkerKit`. Metrics cover those HTTP clients, the runtime, `worker.iterations` (`outcome` = `success` or `failure`), `worker.iteration.duration`, `worker.items.processed` (queue items add `outcome` = `done`, `retry`, or `failed`), `worker.last_success.time`, `worker.queue.pending`, and `worker.health.status`. The VIES sample records `vies.client.duration` on the `WorkerKit.Samples.Vies` meter and registers that name with `builder.AddTelemetrySource`.

Aspire Dashboard is enough for a local look. It keeps data in memory and does not authenticate OTLP, so it is not a production store.

```bash
docker run --rm -d -p 18888:18888 -p 4317:18889 -p 4318:18890 --name aspire-dashboard mcr.microsoft.com/aspire/dashboard:13
```

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 OTEL_EXPORTER_OTLP_PROTOCOL=grpc dotnet run --project src/ExampleHost -- check-vat --country-code DE --vat-number 123456789
```

The UI is at <http://localhost:18888>. Instances are distinguished by `service.instance.id`. A production collector and backend are outside this skeleton. `check-config` prints the service name, version, instance, and whether the OTLP endpoint is set.

## Tests

```bash
dotnet test
```

The suite covers options validation (including the shipped JSON and a command-line override), `check-config`, the version and `check-vat` commands, the Open-Meteo typed client through `IHttpClientFactory` with a stub `HttpMessageHandler`, VIES number normalization and a failed SOAP span, the VIES and outbound queues, MQTT SAS and topic building, the dry-run and Twilio SMS senders, heartbeat staleness, the `/live` and `/ready` status codes and JSON, OpenTelemetry (iteration spans and metrics, health gauge, OTLP enabled only for `run` when an endpoint is set, HttpClient spans), and the in-memory queue (claim, partition lease, retry, dead-letter, shutdown, and the queue item span). Sample tests live in `tests/WorkerKit.Samples.Tests` and use `WorkerKit.Testing`. They do not call VIES, Open-Meteo, Mosquitto, Azure IoT Hub, or Twilio.

## Design decisions

- **One executable, four hosts.** `AddWindowsService` and `AddSystemd` are the current generic-host equivalents of `UseWindowsService` / `UseSystemd`. They no-op when the process is a console app or a container.
- **Serilog only at the host boundary.** Core and Infrastructure log through `ILogger<T>`.
- **Reflection-free commands.** `IWorkerCommand.Spec` is static, so the command tree is built without constructing the command or reflecting over properties. Hosting maps each `CommandOption<T>` and `CommandArgument<T>` onto one System.CommandLine symbol. Handlers take `ICommandArgs` and can be called without the parser. `run` allows unmatched tokens so `--Section:Key value` still reaches configuration; `check-vat` does not, so a typo in `--country-code` is a parse error.
- **Resilience per client, not one global policy.** The standard resilience handler is the supported Polly integration for `IHttpClientFactory`. Our options are a flat subset mapped onto `HttpStandardResilienceOptions`, which is easier to override from the environment than the full Polly graph.
- **SOAP proxy is source, not a build step.** A build that downloads the VIES WSDL fails in any environment that cannot reach it. The generated client stays in `samples/Vies`.
- **Samples are not packages.** `samples/Vies`, `samples/OpenMeteo`, and `samples/Outbound` are `IsPackable` false. CI packs Abstractions, Hosting, Soap, Testing, and Templates only. MQTTnet is referenced from the outbound sample.
- **Database is a connector concern.** The host does not reference a SQL client. `Data:Provider` `Dapper` means the connector registered `IDbConnectionFactory`.
- **The default periodic worker does not call the network.** Open-Meteo runs only when latitude and longitude are set. Both queue samples stay off until `Enabled` is true. SMS defaults to dry-run. MQTT defaults to local Mosquitto.
- **The queue in the skeleton is an abstraction plus an in-memory example.** `IWorkQueue<T>` lives in Core. `InMemoryWorkQueue<T>` is not durable. A RabbitMQ, Azure Service Bus, or SQL implementation belongs in its own project and is registered as `IWorkQueue<T>`.
- **OpenTelemetry exports only OTLP, and only from `run`.** With no `OTEL_EXPORTER_OTLP_ENDPOINT`, the process still records traces and metrics and does not send them. Logs stay on Serilog, with an OTLP sink added beside the file and console sinks.

## Review these choices

- The VIES WSDL is published at `https://ec.europa.eu/taxation_customs/vies/checkVatService.wsdl`. Its `soap:address` is `http://ec.europa.eu/taxation_customs/vies/services/checkVatService`. The sample `Vies:Endpoint` is the HTTPS URL because WCF does not follow the redirect.
- A VAT number is sent without the country prefix. The country code is a separate argument. The WSDL pattern is `[0-9A-Za-z+*.]{2,12}`.
- `OpenMeteo:Resilience:CircuitBreakerMinimumThroughput` is `5` so a sample client can open the breaker. A busy connector should raise it.
- Azure IoT Hub MQTT uses port 8883 and TLS. The sample computes the SAS token from `Mqtt:DeviceSecret`. It does not use the Azure IoT Hub SDK.
- `System.ServiceModel.Http` brings `System.Security.Cryptography.Pkcs` and `System.Security.Cryptography.Xml` 10.0.0, which NuGet audit flags. The projects pin 10.0.13.
- The image runs as uid 1654. Instance data belongs on `/instance`, not under `/app`.
