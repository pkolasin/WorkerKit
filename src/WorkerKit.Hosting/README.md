# WorkerKit.Hosting

Process host for a WorkerKit connector:

- `WorkerApp.CreateBuilder<TProgram>` records commands and `ConfigureHost` delegates, and builds a host only for the command that runs
- translates `IWorkerCommand.Spec` into System.CommandLine without constructing the command, then resolves the command from DI
- built-in commands `run`, `check-config`, `version`, and `help`
- instance directories, single-instance lock, Windows Service and systemd hooks
- Serilog, health checks, and the loopback health endpoint
- OpenTelemetry traces and metrics (`ActivitySource` and `Meter` name `WorkerKit`)
- `PeriodicWorker`, `QueueWorker`, `WorkerContext`, and `InMemoryWorkQueue<T>`
- periodic delays use `TimeProvider` (`WorkerContext.Time`, `TimeProvider.System` unless replaced)
- `IsEnabled` (default true; `QueueWorker` reads `QueueWorkerOptions.Enabled`) stops the hosted loop: one log, no heartbeat, so health is unchanged
- `RunIterationAsync` and `RunBatchAsync` ignore `IsEnabled`, run `OnStartingAsync` once per instance, and do not sleep; `WorkerAppBuilder.BuildHost` builds the collected host without parsing a command
- `AddValidatedOptions`, `AddResilientJsonClient`, `AddQueueWorker`, `AddInMemoryQueueWorker`, `AddTelemetrySource`
- `dotnet publish` of a connector copies `deploy/` scripts and writes `workerkit.app.json` (opt out with `-p:WorkerKitDeployScripts=false`)

The package does not reference a sample connector. Call `ConfigureHost` (or `AddTelemetrySource`) from the application.

The command abstraction lives in `WorkerKit.Abstractions` and does not reference System.CommandLine. This package maps each spec item onto one option or argument.

Repository: https://github.com/pkolasin/WorkerKit
