# Deploy a connector

`dotnet publish` of a project that references `WorkerKit.Hosting` copies the service scripts to `<publish>/deploy/` and writes `<publish>/workerkit.app.json`. The scripts read every name from that file. `-p:WorkerKitDeployScripts=false` skips both.

The manifest is derived the same way as `WorkerAppIdentity`: `WorkerKitAppName` if set, otherwise the `Product` attribute, otherwise the assembly name with a trailing `.Host` removed. `WorkerKitServiceName` defaults to that name. `WorkerKitEnvPrefix` defaults to the name's letters and digits, uppercased. `WorkerKitDisplayName` defaults to the service name. This repo sets `Product` to `WorkerKit` in `Directory.Build.props`, so a connector overrides `Product` or `WorkerKitAppName`.

| Field | Example (`ExampleHost`, product `WorkerKitExample`) |
|---|---|
| `name`, `serviceName`, `displayName` | `WorkerKitExample` |
| `envPrefix` | `WORKERKITEXAMPLE` |
| `version` | informational version, `+` suffix removed |
| `assemblyName`, `linuxExecutable` | `ExampleHost` |
| `windowsExecutable` | `ExampleHost.exe` |
| `assemblyFile` | `ExampleHost.dll` |
| `kebabName` | `worker-kit-example` |
| `windowsInstallPath` | `C:\Program Files\WorkerKitExample` |
| `linuxInstallPath` | `/opt/worker-kit-example` |
| `windowsInstancesRoot` | `%ProgramData%\WorkerKitExample\instances` |
| `linuxInstancesRoot` | `/var/lib/worker-kit-example` |

`install-service.ps1`, `update.ps1`, and `uninstall-service.ps1` share that Windows install path and instance root when the parameters are omitted.

## Windows

Publish the example (self-contained is optional; `-r win-x64` produces `ExampleHost.exe`). Two ways to install:

Publish straight into the install directory, then run the script that landed there:

```powershell
dotnet publish src/ExampleHost/ExampleHost.csproj -c Release -r win-x64 -o "C:\Program Files\WorkerKitExample"
cd "C:\Program Files\WorkerKitExample"
.\deploy\windows\install-service.ps1 -HealthListenUrl "http://127.0.0.1:5088/health/"
```

Or publish to a temporary folder and copy once with `-PublishDir`. That first install is the default instance: it starts immediately, so a later `-PublishDir` is refused while that service (or any other service from the same install directory) is running. `appsettings*.json` files already in the install directory are left as they are.

```powershell
dotnet publish src/ExampleHost/ExampleHost.csproj -c Release -r win-x64 -o C:\publish\WorkerKitExample
cd C:\publish\WorkerKitExample
.\deploy\windows\install-service.ps1 -PublishDir C:\publish\WorkerKitExample -HealthListenUrl "http://127.0.0.1:5088/health/"
```

The default service is `WorkerKitExample`. It runs as LocalSystem. Configuration lives in the install directory. `-HealthListenUrl` is written into `Health:ListenUrl` of that `appsettings.json` as UTF-8 without a BOM (`ConvertTo-Json -Depth 100`, then re-indented with 2 spaces). LocalSystem does not get an HTTP.sys reservation. The service restarts twice, 60 seconds apart, and the failure count resets after one day. Named instances use the same failure actions.

Named instances share that install directory. Install them from the install directory and omit `-PublishDir`, because the copy already happened and the default service is running. Each service is `WorkerKitExample-<instance>`. The script creates the instance directory from `appsettings.instance.template.json` when `appsettings.json` is missing, writes `-HealthListenUrl` there, and does not start the service.

```powershell
cd "C:\Program Files\WorkerKitExample"
.\deploy\windows\install-service.ps1 -Instance ares -HealthListenUrl "http://127.0.0.1:5089/health/"
.\deploy\windows\install-service.ps1 -Instance brno -HealthListenUrl "http://127.0.0.1:5090/health/"
Start-Service WorkerKitExample-ares
Start-Service WorkerKitExample-brno
Get-Service WorkerKitExample*
```

`-ServiceAccount "NT SERVICE"` (no password) configures `NT SERVICE\WorkerKitExample-ares` and reserves the prefix for that account. A domain account takes `-ServiceAccount DOMAIN\svc -ServicePassword '...'`. If another account already holds the prefix, the script warns and leaves the reservation. Omit `-ServiceAccount` to keep LocalSystem.

Update from a new publish directory. The script stops every service whose binary path points at the install, copies files, runs `version`, and starts only the services that were running. It refuses while instance `default` is in use (a service without `--instance`, a live `instance.pid` for this executable that was not started with `--instance-dir`, a `.lock`, or `logs` / `data` / `temp` in the install directory, or an empty instances root). A pid file whose process is gone, whose executable is not this install, or whose command line contains `--instance-dir`, is ignored with a warning. A clean shutdown deletes `instance.pid` before releasing the lock, and only when the file still contains this process's id. `-DefaultInstance` copies binaries only and keeps `appsettings*.json`, `logs`, `data`, and `temp`.

```powershell
.\deploy\windows\update.ps1 -PublishDir C:\publish\WorkerKitExample -DefaultInstance
```

Uninstall removes the service entry and the URL reservation when that account is the only user. Instance files stay.

```powershell
.\deploy\windows\uninstall-service.ps1 -Instance ares
.\deploy\windows\uninstall-service.ps1 -Instance brno
.\deploy\windows\uninstall-service.ps1
```

## Linux

Publish into the install directory, or publish elsewhere and pass `--publish-dir` (existing `appsettings*.json` files are kept; the copy is refused while the unit is active):

```bash
dotnet publish src/ExampleHost/ExampleHost.csproj -c Release -o /tmp/workerkit-example
sudo /tmp/workerkit-example/deploy/systemd/install.sh --publish-dir /tmp/workerkit-example
```

`install.sh` without `--publish-dir` expects the binaries to already be in the install directory (`/opt/worker-kit-example` for this example). The unit user is the service name.

`install.sh` enables `WorkerKitExample.service` only. The template `worker-kit-example@.service` is generated beside it. For named instances, create the kebab user once and enable the template:

```bash
sudo useradd --system --home /var/lib/worker-kit-example --shell /usr/sbin/nologin worker-kit-example
sudo install -m 0644 /opt/worker-kit-example/deploy/systemd/worker-kit-example@.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now worker-kit-example@ares
sudo systemctl enable --now worker-kit-example@brno
```

`StateDirectory=worker-kit-example/%i` creates `/var/lib/worker-kit-example/%i`. `ExecStart` runs `ExampleHost` with `--instance-dir` and `--instance`.

Update follows the same default-instance rule as Windows. `--default-instance` keeps `appsettings*.json` and instance data. `--dry-run` prints the `rsync` and version command and does not call systemd.

```bash
sudo /opt/worker-kit-example/deploy/systemd/update.sh /tmp/workerkit-example --default-instance
```

Uninstall disables the units and leaves instance data:

```bash
sudo systemctl disable --now worker-kit-example@ares worker-kit-example@brno WorkerKitExample.service
sudo rm -f /etc/systemd/system/worker-kit-example@.service /etc/systemd/system/WorkerKitExample.service
sudo systemctl daemon-reload
```

`--install-dir` on `install.sh` and `update.sh` overrides the manifest path. `install.sh --dry-run` prints the `useradd` and `systemctl` steps.
