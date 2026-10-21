---
name: workerkit-connector
description: Scaffold or extend a WorkerKit connector from the NuGet packages (WorkerKit.Hosting, WorkerKit.Abstractions, WorkerKit.Testing, optional WorkerKit.Soap). Use when asked to add a periodic worker, a queue worker, a CLI command, or a new dotnet new workerkit-connector app.
---

# WorkerKit connector

Create the app with the template package, then follow the generated `AGENTS.md`. That file has the project name, environment prefix, and target framework already substituted.

```bash
dotnet new install WorkerKit.Templates
dotnet new workerkit-connector -n Contoso.Billing -o ../Contoso.Billing
```

`-n` is the namespace and product name (`^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$`). Optional: `--framework net8.0`, `--env-prefix`, `--queue`, `--docker false`, `--workerkit-version`.

Packages: Hosting (host, workers, CLI), Abstractions (command and queue contracts), Testing (tests only), Soap (WCF binding helpers only), Sql (optional SQL Server and PostgreSQL dialects, leases, bulk copy, and migrations). They multi-target `net8.0` and `net10.0`. A `net9.0` app uses the `net8.0` assets. Call `AddWorkerKitSql` for `Data:Provider` `SqlServer` or `PostgreSQL`. That value is read when the dialect is resolved. A large download uses `AddStreamingClient`.

Rules that are easy to get wrong:

- Register commands with `app.Commands.Add<T>()` and a static `CommandSpec`. Do not reflect over options. Root names `run`, `check-config`, `version`, and `help` are reserved.
- Subclass `PeriodicWorker` or `QueueWorker<T>`. Take scoped services from the iteration's `IServiceProvider`, not the constructor.
- After a command change, regenerate `doc/cli.md` and keep `CliReference.AssertUpToDate` green.
- `run` records telemetry. Export happens only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

Index of the package docs: https://github.com/pkolasin/WorkerKit/blob/main/llms.txt
