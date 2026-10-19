using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting.Tests;

public sealed class InstanceLocatorTests
{
    [Fact]
    public void Nothing_set_uses_the_default_instance_in_the_install_directory()
    {
        var location = InstanceLocator.Resolve([], _ => null, "/opt/Contoso");

        Assert.Equal("default", location.Name);
        Assert.True(location.IsDefault);
        Assert.Equal(Path.GetFullPath("/opt/Contoso"), location.Directory);
    }

    [Fact]
    public void Instance_dir_argument_wins_over_the_environment()
    {
        var location = InstanceLocator.Resolve(
            ["--instance-dir", "/data/explicit"],
            name => name == InstanceLocator.DirEnv ? "/data/from-env" : null,
            "/opt/Contoso");

        Assert.False(location.IsDefault);
        Assert.Equal(Path.GetFullPath("/data/explicit"), location.Directory);
        Assert.Equal("explicit", location.Name);
    }

    [Fact]
    public void Environment_directory_is_used_when_the_argument_is_absent()
    {
        var location = InstanceLocator.Resolve(
            ["--instance", "vat"],
            name => name == InstanceLocator.DirEnv ? "/data/custom" : null,
            "/opt/Contoso");

        Assert.Equal("vat", location.Name);
        Assert.Equal(Path.GetFullPath("/data/custom"), location.Directory);
    }

    [Fact]
    public void Instance_name_combines_with_the_instances_root()
    {
        var location = InstanceLocator.Resolve(
            ["--instance=ares"],
            name => name == InstanceLocator.RootEnv ? "/data/instances" : null,
            "/opt/Contoso");

        Assert.Equal("ares", location.Name);
        Assert.Equal(Path.GetFullPath(Path.Combine("/data/instances", "ares")), location.Directory);
    }

    [Fact]
    public void Environment_name_uses_the_default_root()
    {
        var location = InstanceLocator.Resolve(
            [],
            name => name == InstanceLocator.NameEnv ? "vat" : null,
            "/opt/Contoso");

        var expectedRoot = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WorkerKit", "instances")
            : "/var/lib/worker-kit";
        Assert.Equal("vat", location.Name);
        Assert.Equal(Path.GetFullPath(Path.Combine(expectedRoot, "vat")), location.Directory);
    }

    [Fact]
    public void Explicit_name_is_kept_when_the_directory_has_another_name()
    {
        var location = InstanceLocator.Resolve(
            ["--instance-dir=/data/folder", "--instance", "vat"],
            _ => null,
            "/opt/Contoso");

        Assert.Equal("vat", location.Name);
        Assert.Equal(Path.GetFullPath("/data/folder"), location.Directory);
    }

    [Fact]
    public void Relative_instance_dir_is_normalized()
    {
        var previous = Directory.GetCurrentDirectory();
        var root = Directory.CreateTempSubdirectory("ws-cwd");
        var instance = Directory.CreateDirectory(Path.Combine(root.FullName, "ares"));
        try
        {
            Directory.SetCurrentDirectory(instance.FullName);
            var location = InstanceLocator.Resolve(["--instance-dir", "."], _ => null, "/opt/Contoso");

            Assert.Equal("ares", location.Name);
            Assert.Equal(Path.GetFullPath(instance.FullName), location.Directory);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    [Fact]
    public void Blank_environment_values_are_ignored()
    {
        var location = InstanceLocator.Resolve(
            [],
            name => name == InstanceLocator.DirEnv || name == InstanceLocator.NameEnv ? "   " : null,
            "/opt/Contoso");

        Assert.True(location.IsDefault);
        Assert.Equal("default", location.Name);
    }

    [Fact]
    public void A_longer_option_name_is_not_treated_as_instance_dir()
    {
        var location = InstanceLocator.Resolve(
            ["--instance-directory", "/data/nope"],
            _ => null,
            "/opt/Contoso");

        Assert.True(location.IsDefault);
    }

    [Theory]
    [InlineData("Ares")]
    [InlineData("has_underscore")]
    [InlineData("-leading-hyphen")]
    [InlineData("")]
    public void Invalid_instance_name_is_rejected(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            InstanceLocator.Resolve(["--instance", name], _ => null, "/opt/Contoso"));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_option_value_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            InstanceLocator.Resolve(["--instance-dir"], _ => null, "/opt/Contoso"));
    }

    [Fact]
    public void Service_name_keeps_the_single_service_name_for_default()
    {
        Assert.Equal("WorkerKit", WorkerHostDefaults.ServiceNameFor("default"));
        Assert.Equal("WorkerKit-ares", WorkerHostDefaults.ServiceNameFor("ares"));
    }
}

public sealed class InstanceConfigurationTests
{
    [Fact]
    public void Later_layers_override_earlier_ones()
    {
        using var layout = InstanceLayout.Create();
        layout.Write(layout.Install, "appsettings.json", new { Worker = new { ExampleIco = "install" }, Probe = "install" });
        layout.Write(layout.Install, "appsettings.Production.json", new { Probe = "install-env" });
        layout.Write(layout.Instance, "appsettings.json", new { Worker = new { ExampleIco = "instance" }, Probe = "instance" });
        layout.Write(layout.Instance, "appsettings.Production.json", new { Probe = "instance-env" });

        using var host = layout.BuildProduction();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("instance-env", configuration["Probe"]);
        Assert.Equal("instance", configuration["Worker:ExampleIco"]);
    }

    [Fact]
    public void Instance_json_overrides_install_environment_json()
    {
        using var layout = InstanceLayout.Create();
        layout.Write(layout.Install, "appsettings.json", new { Probe = "install" });
        layout.Write(layout.Install, "appsettings.Production.json", new { Probe = "install-env" });
        layout.Write(layout.Instance, "appsettings.json", new { Probe = "instance" });

        using var host = layout.BuildProduction();

        Assert.Equal("instance", host.Services.GetRequiredService<IConfiguration>()["Probe"]);
    }

    [Fact]
    public void Environment_and_command_line_override_instance_json()
    {
        using var layout = InstanceLayout.Create();
        layout.Write(layout.Install, "appsettings.json", new { Probe = "install", Worker = new { ExampleIco = "install" } });
        layout.Write(layout.Instance, "appsettings.json", new { Probe = "instance", Worker = new { ExampleIco = "instance" } });

        var previous = Environment.GetEnvironmentVariable("Probe");
        Environment.SetEnvironmentVariable("Probe", "from-env");
        try
        {
            using (var host = layout.BuildProduction())
            {
                Assert.Equal("from-env", host.Services.GetRequiredService<IConfiguration>()["Probe"]);
            }

            using var overridden = layout.BuildProduction("--Probe", "from-cmd");
            var configuration = overridden.Services.GetRequiredService<IConfiguration>();
            Assert.Equal("from-cmd", configuration["Probe"]);
            Assert.Equal("instance", configuration["Worker:ExampleIco"]);
            Assert.Equal("ares", overridden.Services.GetRequiredService<IInstanceContext>().InstanceName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Probe", previous);
        }
    }

    [Fact]
    public void Relative_paths_resolve_against_the_instance_directory_and_absolute_paths_do_not()
    {
        using var layout = InstanceLayout.Create();
        var absoluteLog = Path.Combine(layout.Root, "absolute", "worker-.json");
        layout.Write(layout.Install, "appsettings.json", new
        {
            LogFile = new { Path = "logs/worker-.json" },
            Instance = new { DataDirectory = "data", TempDirectory = "temp" },
        });
        layout.Write(layout.Instance, "appsettings.json", new
        {
            LogFile = new { Path = absoluteLog },
            Instance = new { DataDirectory = "batches", TempDirectory = "scratch" },
        });

        using var host = layout.BuildProduction();
        var instance = host.Services.GetRequiredService<IInstanceContext>();

        Assert.Equal("ares", instance.InstanceName);
        Assert.Equal(Path.GetFullPath(layout.Install), instance.InstallDirectory);
        Assert.Equal(Path.GetFullPath(layout.Instance), instance.InstanceDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(layout.Instance, "batches")), instance.DataDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(layout.Instance, "scratch")), instance.TempDirectory);
        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(absoluteLog)), instance.LogDirectory);
        Assert.Equal(Path.GetFullPath(absoluteLog), instance.ResolvePath(absoluteLog));
    }

    [Fact]
    public void Default_instance_resolves_paths_against_the_install_directory()
    {
        var install = Directory.CreateTempSubdirectory("ws-default").FullName;
        using var host = WorkerHostBuilder.Create([], contentRoot: install, environmentName: "Production").Build();
        var instance = host.Services.GetRequiredService<IInstanceContext>();

        Assert.Equal("default", instance.InstanceName);
        Assert.Equal(Path.GetFullPath(install), instance.InstanceDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(install, "data")), instance.DataDirectory);
        Assert.Equal(Path.GetFullPath(Path.Combine(install, "logs")), instance.LogDirectory);
    }

    [Fact]
    public void Clean_temp_on_start_removes_only_temp_children()
    {
        using var layout = InstanceLayout.Create();
        layout.Write(layout.Install, "appsettings.json", new { });
        Directory.CreateDirectory(Path.Combine(layout.Instance, "temp"));
        Directory.CreateDirectory(Path.Combine(layout.Instance, "data"));
        File.WriteAllText(Path.Combine(layout.Instance, "temp", "scratch.txt"), "gone");
        File.WriteAllText(Path.Combine(layout.Instance, "data", "keep.txt"), "stay");

        using var host = layout.BuildProduction();

        Assert.False(File.Exists(Path.Combine(layout.Instance, "temp", "scratch.txt")));
        Assert.Equal("stay", File.ReadAllText(Path.Combine(layout.Instance, "data", "keep.txt")));
        _ = host;
    }

    [Fact]
    public void Clean_temp_on_start_refuses_to_wipe_the_instance_directory()
    {
        using var layout = InstanceLayout.Create();
        layout.Write(layout.Instance, "appsettings.json", new
        {
            Instance = new { TempDirectory = layout.Instance, CleanTempOnStart = true },
        });
        File.WriteAllText(Path.Combine(layout.Instance, "keep.txt"), "stay");

        var exception = Assert.Throws<InvalidOperationException>(() => layout.BuildProduction());

        Assert.Contains("Refusing to clean", exception.Message, StringComparison.Ordinal);
        Assert.Equal("stay", File.ReadAllText(Path.Combine(layout.Instance, "keep.txt")));
    }

    [Fact]
    public void Identical_data_and_temp_directories_fail_validation()
    {
        var install = Directory.CreateTempSubdirectory("ws-instance-options").FullName;
        using var host = WorkerHostBuilder.Create(
            [],
            contentRoot: install,
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Instance:DataDirectory"] = "same",
                    ["Instance:TempDirectory"] = "same",
                });
            }).Build();

        var failures = StartupValidation.GetFailures(host.Services);

        Assert.Contains(failures, failure => failure.Contains("must be different", StringComparison.Ordinal));
    }
}

public sealed class InstanceLockTests
{
    [Fact]
    public void A_second_acquire_fails_until_the_first_lock_is_released()
    {
        var directory = Directory.CreateTempSubdirectory("ws-lock").FullName;
        var instance = InstanceEnvironment.Create(
            new InstanceLocation("ares", directory, false),
            directory,
            new ConfigurationBuilder().Build());

        using (var first = InstanceLock.Acquire(instance))
        {
            Assert.Equal(
                Environment.ProcessId.ToString(),
                File.ReadAllText(Path.Combine(directory, "instance.pid")));
            var exception = Assert.Throws<InvalidOperationException>(() => InstanceLock.Acquire(instance));
            Assert.Contains("ares", exception.Message, StringComparison.Ordinal);
            Assert.Contains(".lock", exception.Message, StringComparison.Ordinal);
            _ = first;
        }

        Assert.False(File.Exists(Path.Combine(directory, ".lock")));
        Assert.False(File.Exists(Path.Combine(directory, "instance.pid")));
        using var second = InstanceLock.Acquire(instance);
        Assert.True(File.Exists(Path.Combine(directory, ".lock")));
        Assert.Equal(Environment.ProcessId.ToString(), File.ReadAllText(Path.Combine(directory, "instance.pid")).Trim());
    }

    [Fact]
    public void Dispose_leaves_a_pid_file_that_belongs_to_another_process()
    {
        var directory = Directory.CreateTempSubdirectory("ws-lock-pid").FullName;
        var instance = InstanceEnvironment.Create(
            new InstanceLocation("ares", directory, false),
            directory,
            new ConfigurationBuilder().Build());
        var pidPath = Path.Combine(directory, "instance.pid");

        var held = InstanceLock.Acquire(instance);
        File.WriteAllText(pidPath, "999999");
        held.Dispose();

        Assert.False(File.Exists(Path.Combine(directory, ".lock")));
        Assert.Equal("999999", File.ReadAllText(pidPath).Trim());
    }
}

public sealed class InstanceCommandTests
{
    [Fact]
    public void Check_config_prints_instance_directories()
    {
        var install = Directory.CreateTempSubdirectory("ws-check-instance").FullName;
        var output = new RecordingOutput();
        using var host = WorkerHostBuilder.Create(
            [],
            contentRoot: install,
            environmentName: "Production",
            configure: builder => builder.Services.AddSingleton<ICommandOutput>(output)).Build();

        var code = host.Services.GetRequiredService<CheckConfigHandler>().Execute();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(output.Lines, line => line == "Instance: default");
        Assert.Contains(output.Lines, line => line == "Install directory: " + Path.GetFullPath(install));
        Assert.Contains(output.Lines, line => line == "Instance directory: " + Path.GetFullPath(install));
        Assert.Contains(output.Lines, line => line.StartsWith("Log directory: ", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.StartsWith("Data directory: ", StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line.StartsWith("Log file: ", StringComparison.Ordinal));
    }

}

internal sealed class InstanceLayout : IDisposable
{
    private InstanceLayout(string root, string install, string instance)
    {
        Root = root;
        Install = install;
        Instance = instance;
    }

    public string Root { get; }

    public string Install { get; }

    public string Instance { get; }

    public static InstanceLayout Create()
    {
        var root = Directory.CreateTempSubdirectory("ws-instance").FullName;
        var install = Path.Combine(root, "install");
        var instance = Path.Combine(root, "ares");
        Directory.CreateDirectory(install);
        Directory.CreateDirectory(instance);
        return new InstanceLayout(root, install, instance);
    }

    public void Write(string directory, string fileName, object value)
    {
        File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(value));
    }

    public IHost BuildProduction(params string[] extraArgs) => CreateHost("Production", extraArgs);

    public IHost BuildDevelopment() => CreateHost("Development", []);

    private IHost CreateHost(string environmentName, string[] extraArgs)
    {
        var args = new List<string> { "--instance-dir", Instance, "--instance", "ares" };
        args.AddRange(extraArgs);
        return WorkerHostBuilder.Create(args.ToArray(), contentRoot: Install, environmentName: environmentName).Build();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Serilog may still be releasing the log file.
        }
    }
}

