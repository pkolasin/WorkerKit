# Contoso.Billing

WorkerKit connector. The sample worker and the sample HTTP client stay offline until `Sample:Enabled` is true and `Sample:ItemId` is set. Replace them with the real connector.

## Implement a connector

1. Add the connector's options next to `SampleWorkerOptions` and bind them with `AddValidatedOptions`.
2. Replace `SamplePollingWorker`. It receives `WorkerContext` and reads `IsEnabled` from options. Register it from `AddContosoBillingConnector`.
3. Replace `SampleCommand`, or add another `IWorkerCommand`, and register it with `app.Commands.Add` in `Program.cs`.
4. Point `SampleApiOptions.BaseAddress` at the real endpoint. `AddResilientJsonClient` registers the typed client. Leave `Sample:Enabled` false until the call should run.
5. `dotnet test`, then `dotnet publish src/Contoso.Billing.Host -c Release -o artifacts/publish`. The WorkerKit.Hosting package writes `deploy/` and `workerkit.app.json` into that folder. Install from `deploy/windows` or `deploy/systemd`. Names, paths, and the environment prefix come from `workerkit.app.json`. The service scripts are documented at https://github.com/pkolasin/WorkerKit/blob/main/doc/deploy.md.

`dotnet run --project src/Contoso.Billing.Host -- check-config` prints the resolved configuration without taking the instance lock.

## Local WorkerKit checkout

`-p:UseLocalWorkerKit=<path>` is the directory that contains `src/WorkerKit.Hosting`. The host and the tests then use `ProjectReference` instead of the WorkerKit packages. Publish still copies `deploy/` because `Directory.Build.targets` imports the hosting targets from that checkout.

```bash
dotnet test -p:UseLocalWorkerKit=../WorkerKit
```

## Parameters used for this repo

The name is `Contoso.Billing`. The environment prefix is `CONTOSOBILLING` (`CONTOSOBILLING_INSTANCE_DIR` in Docker). The target framework is `__TARGET_FRAMEWORK__`.
