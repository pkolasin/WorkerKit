using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text.Json;
using ExampleHost.Composition;
using WorkerKit.Abstractions.Identity;

namespace ExampleHost.Tests;

public sealed class DeployScriptsPublishFixture : IDisposable
{
    public DeployScriptsPublishFixture()
    {
        PublishDir = Path.Combine(Path.GetTempPath(), "wk-deploy-" + Guid.NewGuid().ToString("N"));
        var project = RepoPaths.RepoFile("src/ExampleHost/ExampleHost.csproj");
        var result = DeployPublish.Publish(project, PublishDir);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("dotnet publish failed: " + result.Output);
        }
    }

    public string PublishDir { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(PublishDir))
            {
                Directory.Delete(PublishDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

public sealed class DeployScriptsPublishTests : IClassFixture<DeployScriptsPublishFixture>
{
    private readonly DeployScriptsPublishFixture _fixture;

    public DeployScriptsPublishTests(DeployScriptsPublishFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Publish_writes_deploy_scripts_and_a_manifest_that_matches_WorkerAppIdentity()
    {
        var publishDir = _fixture.PublishDir;
        var windows = Path.Combine(publishDir, "deploy", "windows");
        var systemd = Path.Combine(publishDir, "deploy", "systemd");
        Assert.True(File.Exists(Path.Combine(windows, "install-service.ps1")));
        Assert.True(File.Exists(Path.Combine(windows, "update.ps1")));
        Assert.True(File.Exists(Path.Combine(windows, "uninstall-service.ps1")));
        Assert.True(File.Exists(Path.Combine(windows, "HttpUrlAcl.ps1")));
        Assert.True(File.Exists(Path.Combine(windows, "WorkerKitApp.ps1")));
        Assert.False(File.Exists(Path.Combine(windows, "HttpUrlAcl.Tests.ps1")));
        Assert.True(File.Exists(Path.Combine(systemd, "install.sh")));
        Assert.True(File.Exists(Path.Combine(systemd, "update.sh")));
        Assert.True(File.Exists(Path.Combine(systemd, "workerkit-app.sh")));

        var identity = WorkerAppIdentity.FromAssembly(typeof(ExampleConnectorExtensions).Assembly);
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(publishDir, "workerkit.app.json")));
        var root = document.RootElement;
        Assert.Equal(identity.Name, Field(root, "name"));
        Assert.Equal(identity.ServiceName, Field(root, "serviceName"));
        Assert.Equal(identity.ServiceName, Field(root, "displayName"));
        Assert.Equal(identity.EnvPrefix, Field(root, "envPrefix"));
        Assert.Equal(identity.Version, Field(root, "version"));
        var assemblyName = identity.ApplicationAssembly.GetName().Name;
        Assert.Equal(assemblyName, Field(root, "assemblyName"));
        Assert.Equal(assemblyName + ".exe", Field(root, "windowsExecutable"));
        Assert.Equal(assemblyName, Field(root, "linuxExecutable"));
        Assert.Equal(assemblyName + ".dll", Field(root, "assemblyFile"));
        var kebab = WorkerAppIdentity.ToKebabCase(identity.Name);
        Assert.Equal(kebab, Field(root, "kebabName"));
        Assert.Equal(@"C:\Program Files\" + identity.Name, Field(root, "windowsInstallPath"));
        Assert.Equal("/opt/" + kebab, Field(root, "linuxInstallPath"));
        Assert.Equal(@"%ProgramData%\" + identity.Name + @"\instances", Field(root, "windowsInstancesRoot"));
        Assert.Equal(identity.LinuxInstancesRoot, Field(root, "linuxInstancesRoot"));

        var defaultUnit = File.ReadAllText(Path.Combine(systemd, identity.ServiceName + ".service"));
        Assert.Contains("User=" + identity.ServiceName, defaultUnit, StringComparison.Ordinal);
        Assert.Contains("ExecStart=\"/opt/" + kebab + "/" + assemblyName + "\" run", defaultUnit, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", defaultUnit, StringComparison.Ordinal);
        var templateUnit = File.ReadAllText(Path.Combine(systemd, kebab + "@.service"));
        Assert.Contains("User=" + kebab, templateUnit, StringComparison.Ordinal);
        Assert.Contains("--instance %i", templateUnit, StringComparison.Ordinal);
        Assert.Contains("StateDirectory=" + kebab + "/%i", templateUnit, StringComparison.Ordinal);

        foreach (var script in Directory.GetFiles(windows, "*.ps1").Concat(Directory.GetFiles(systemd)))
        {
            var text = File.ReadAllText(script);
            Assert.DoesNotContain("WorkerSkeleton", text, StringComparison.Ordinal);
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(Path.Combine(systemd, "install.sh")).HasFlag(UnixFileMode.UserExecute));
            Assert.True(File.GetUnixFileMode(Path.Combine(systemd, "update.sh")).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Fact]
    public void PowerShell_scripts_parse_and_url_acl_checks_pass()
    {
        var root = RepoPaths.RepoFile("src/WorkerKit.Hosting/build/deploy");
        var scripts = Directory.GetFiles(root, "*.ps1", SearchOption.AllDirectories);
        Assert.NotEmpty(scripts);
        foreach (var script in scripts)
        {
            var ast = Parser.ParseFile(script, out _, out var errors);
            Assert.True(errors is null || errors.Length == 0, script + ": " + string.Join("; ", errors?.Select(error => error.ToString()) ?? []));
            foreach (var node in ast.FindAll(_ => true, true))
            {
                var typeName = node.GetType().Name;
                Assert.False(
                    typeName is "TernaryExpressionAst" or "PipelineChainAst" or "NullCoalescingExpressionAst",
                    script + " uses " + typeName + ", which Windows PowerShell 5.1 cannot parse.");
            }
        }

        var testScript = Path.Combine(root, "windows", "HttpUrlAcl.Tests.ps1");
        using var powershell = PowerShell.Create();
        powershell.AddCommand(testScript);
        var results = powershell.Invoke();
        var text = string.Join(
            Environment.NewLine,
            results.Select(result => result?.ToString())
                .Concat(powershell.Streams.Information.Select(record => record.MessageData?.ToString()))
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        // Caught throws inside the script stay on the error stream, so HadErrors is not the result.
        Assert.Contains("HttpUrlAcl checks passed.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shellcheck_accepts_the_bash_scripts_when_it_is_installed()
    {
        var shellcheck = DeployPublish.FindOnPath("shellcheck");
        if (shellcheck is null)
        {
            return;
        }

        var directory = RepoPaths.RepoFile("src/WorkerKit.Hosting/build/deploy/systemd");
        var scripts = Directory.GetFiles(directory, "*.sh");
        var arguments = new List<string> { "-x", "-P", "\"" + directory + "\"" };
        arguments.AddRange(scripts.Select(script => "\"" + script + "\""));
        var result = DeployPublish.Run(shellcheck, "", arguments);
        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Fact]
    public void Systemd_update_dry_run_refuses_a_default_instance_and_keeps_appsettings()
    {
        var publishDir = _fixture.PublishDir;
        var update = Path.Combine(publishDir, "deploy", "systemd", "update.sh");
        var install = Path.Combine(publishDir, "deploy", "systemd", "install.sh");
        var refused = DeployPublish.Run(
            "bash",
            "",
            [update, publishDir, "--install-dir", Path.Combine(Path.GetTempPath(), "wk-missing-" + Guid.NewGuid().ToString("N")), "--instances-root", Path.Combine(Path.GetTempPath(), "wk-instances-" + Guid.NewGuid().ToString("N"))]);
        Assert.Equal(1, refused.ExitCode);
        Assert.Contains("Legacy unit without --instance-dir: none", refused.Output, StringComparison.Ordinal);
        Assert.Contains("Traces: none", refused.Output, StringComparison.Ordinal);
        Assert.Contains("Use --default-instance", refused.Output, StringComparison.Ordinal);

        var installDir = Path.Combine(Path.GetTempPath(), "wk-install-" + Guid.NewGuid().ToString("N"));
        var instancesRoot = Path.Combine(installDir, "instances");
        Directory.CreateDirectory(Path.Combine(instancesRoot, "ares"));
        var settings = Path.Combine(installDir, "appsettings.json");
        File.WriteAllText(settings, "{\"Health\":{\"ListenUrl\":\"\"}}\n");
        File.WriteAllText(Path.Combine(installDir, "instance.pid"), "1\n");
        Process? sleeper = null;
        try
        {
            var stale = DeployPublish.Run("bash", "", [update, publishDir, "--install-dir", installDir, "--instances-root", instancesRoot, "--dry-run"]);
            Assert.True(stale.ExitCode == 0, stale.Output);
            Assert.Contains("Ignoring stale instance.pid", stale.Output, StringComparison.Ordinal);
            Assert.Contains("dry-run: rsync", stale.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("is in use", stale.Output, StringComparison.Ordinal);

            var exe = Path.Combine(installDir, "ExampleHost");
            File.Copy("/bin/bash", exe);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            sleeper = StartBash(exe, namedInstance: true);
            File.WriteAllText(Path.Combine(installDir, "instance.pid"), sleeper.Id + "\n");
            var named = DeployPublish.Run("bash", "", [update, publishDir, "--install-dir", installDir, "--instances-root", instancesRoot, "--dry-run"]);
            Assert.True(named.ExitCode == 0, named.Output);
            Assert.Contains("started with --instance-dir", named.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("is in use", named.Output, StringComparison.Ordinal);
            StopProcess(sleeper);
            sleeper = null;

            sleeper = StartBash(exe, namedInstance: false);
            File.WriteAllText(Path.Combine(installDir, "instance.pid"), sleeper.Id + "\n");
            var live = DeployPublish.Run("bash", "", [update, publishDir, "--install-dir", installDir, "--instances-root", instancesRoot]);
            Assert.Equal(1, live.ExitCode);
            Assert.Contains("instance.pid", live.Output, StringComparison.Ordinal);
            Assert.Contains("Use --default-instance", live.Output, StringComparison.Ordinal);

            var updated = DeployPublish.Run("bash", "", [update, publishDir, "--install-dir", installDir, "--instances-root", instancesRoot, "--default-instance", "--dry-run"]);
            Assert.True(updated.ExitCode == 0, updated.Output);
            Assert.Contains("dry-run: rsync", updated.Output, StringComparison.Ordinal);
            Assert.Equal("{\"Health\":{\"ListenUrl\":\"\"}}\n", File.ReadAllText(settings));

            var planned = DeployPublish.Run("bash", "", [install, "--install-dir", publishDir, "--dry-run"]);
            Assert.True(planned.ExitCode == 0, planned.Output);
            Assert.Contains("dry-run: systemctl enable --now WorkerKitExample.service", planned.Output, StringComparison.Ordinal);
            Assert.Contains("worker-kit-example@<instance>", planned.Output, StringComparison.Ordinal);

            var copied = DeployPublish.Run("bash", "", [install, "--publish-dir", publishDir, "--install-dir", installDir, "--dry-run"]);
            Assert.True(copied.ExitCode == 0, copied.Output);
            Assert.Contains("dry-run: rsync", copied.Output, StringComparison.Ordinal);
            Assert.Contains("appsettings.json", copied.Output, StringComparison.Ordinal);
        }
        finally
        {
            if (sleeper is not null && !sleeper.HasExited)
            {
                sleeper.Kill(entireProcessTree: true);
                sleeper.WaitForExit();
            }

            sleeper?.Dispose();
            Directory.Delete(installDir, recursive: true);
        }
    }

    [Fact]
    public void Opt_out_publish_omits_scripts_and_an_app_name_override_is_written()
    {
        var project = RepoPaths.RepoFile("src/ExampleHost/ExampleHost.csproj");
        var optedOut = Path.Combine(Path.GetTempPath(), "wk-optout-" + Guid.NewGuid().ToString("N"));
        var overridden = Path.Combine(Path.GetTempPath(), "wk-acme-" + Guid.NewGuid().ToString("N"));
        try
        {
            var skipped = DeployPublish.Publish(project, optedOut, ["-p:WorkerKitDeployScripts=false"]);
            Assert.True(skipped.ExitCode == 0, skipped.Output);
            Assert.False(File.Exists(Path.Combine(optedOut, "workerkit.app.json")));
            Assert.False(File.Exists(Path.Combine(optedOut, "deploy", "windows", "install-service.ps1")));

            var renamed = DeployPublish.Publish(project, overridden, ["-p:WorkerKitAppName=Acme"]);
            Assert.True(renamed.ExitCode == 0, renamed.Output);
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(overridden, "workerkit.app.json")));
            var root = document.RootElement;
            var identity = WorkerAppIdentity.Create(typeof(ExampleConnectorExtensions).Assembly, name: "Acme");
            Assert.Equal(identity.Name, Field(root, "name"));
            Assert.Equal(identity.ServiceName, Field(root, "serviceName"));
            Assert.Equal(identity.EnvPrefix, Field(root, "envPrefix"));
            Assert.Equal("ExampleHost.exe", Field(root, "windowsExecutable"));
            Assert.Equal("/opt/acme", Field(root, "linuxInstallPath"));
            Assert.Equal(@"C:\Program Files\Acme", Field(root, "windowsInstallPath"));
            Assert.Equal(identity.LinuxInstancesRoot, Field(root, "linuxInstancesRoot"));
        }
        finally
        {
            if (Directory.Exists(optedOut))
            {
                Directory.Delete(optedOut, recursive: true);
            }

            if (Directory.Exists(overridden))
            {
                Directory.Delete(overridden, recursive: true);
            }
        }
    }

    private static string Field(JsonElement root, string name) => root.GetProperty(name).GetString()!;

    private static Process StartBash(string exe, bool namedInstance)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        start.ArgumentList.Add("-c");
        // A trailing builtin keeps bash from exec'ing sleep, so the command line still contains --instance-dir.
        start.ArgumentList.Add("sleep 30; true");
        if (namedInstance)
        {
            start.ArgumentList.Add("--");
            start.ArgumentList.Add("--instance-dir");
            start.ArgumentList.Add("/tmp/ares");
        }

        return Process.Start(start) ?? throw new InvalidOperationException("Could not start " + exe);
    }

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        finally
        {
            process.Dispose();
        }
    }
}

internal static class DeployPublish
{
    public static (int ExitCode, string Output) Publish(string project, string output, IEnumerable<string>? extra = null)
    {
        var arguments = new List<string>
        {
            "publish",
            "\"" + project + "\"",
            "-c",
            "Debug",
            "-o",
            "\"" + output + "\"",
            "--nologo",
        };
        if (extra is not null)
        {
            arguments.AddRange(extra);
        }

        return Run("dotnet", RepoPaths.RepoFile("."), arguments);
    }

    public static (int ExitCode, string Output) Run(string fileName, string workingDirectory, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(fileName, string.Join(" ", arguments))
        {
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "wk-cli-home");
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + fileName);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(3)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(fileName + " did not exit within 3 minutes.");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    public static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
