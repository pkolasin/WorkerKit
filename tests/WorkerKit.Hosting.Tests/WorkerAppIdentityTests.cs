using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting;
using WorkerKit.Hosting.Telemetry;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting.Tests;

public sealed class WorkerAppIdentityTests
{
    [Fact]
    public void Entry_assembly_under_the_test_host_falls_back_to_the_host_assembly()
    {
        var fallback = typeof(WorkerHostBuilder).Assembly;
        var fromEntry = WorkerAppIdentity.FromEntryAssembly(fallback);
        var fromHost = WorkerAppIdentity.FromAssembly(fallback);

        Assert.Equal(fromHost, fromEntry);
        Assert.Same(fallback, fromEntry.ApplicationAssembly);
    }

    [Fact]
    public void Library_assembly_uses_the_workerkit_identity()
    {
        var identity = WorkerAppIdentity.FromAssembly(typeof(WorkerHostBuilder).Assembly);

        Assert.Equal("WorkerKit", identity.Name);
        Assert.Equal("WorkerKit", identity.ServiceName);
        Assert.Equal("WORKERKIT", identity.EnvPrefix);
        Assert.Equal("WORKERKIT_INSTANCE", identity.InstanceVariable);
        Assert.Equal("WORKERKIT_INSTANCE_DIR", identity.InstanceDirectoryVariable);
        Assert.Equal("WORKERKIT_INSTANCES_ROOT", identity.InstancesRootVariable);
        Assert.Equal("/var/lib/worker-kit", identity.LinuxInstancesRoot);
        Assert.EndsWith(Path.Combine("WorkerKit", "instances"), identity.WindowsInstancesRoot);
        Assert.Equal("0.1.1", identity.Version);
        Assert.Equal("WorkerKit", WorkerHostDefaults.ServiceNameFor("default"));
        Assert.Equal("WorkerKit-east", WorkerHostDefaults.ServiceNameFor("east"));
    }

    [Theory]
    [InlineData("ContosoBilling", "CONTOSOBILLING")]
    [InlineData("Northwind.Shipping", "NORTHWINDSHIPPING")]
    public void Env_prefix_keeps_letters_and_digits(string name, string prefix)
    {
        Assert.Equal(prefix, WorkerAppIdentity.DeriveEnvPrefix(name));
    }

    [Theory]
    [InlineData("ContosoBilling", "contoso-billing")]
    [InlineData("Northwind.Shipping", "northwind-shipping")]
    [InlineData("HTTPServer", "http-server")]
    public void Linux_root_uses_kebab_case(string name, string kebab)
    {
        Assert.Equal(kebab, WorkerAppIdentity.ToKebabCase(name));
    }

    [Fact]
    public void Explicit_override_replaces_the_name_and_the_env_prefix()
    {
        var identity = WorkerAppIdentity.Create(
            typeof(WorkerHostBuilder).Assembly,
            name: "Northwind.Shipping",
            serviceName: "NorthwindShipping",
            envPrefix: "nwship");

        Assert.Equal("Northwind.Shipping", identity.Name);
        Assert.Equal("NorthwindShipping", identity.ServiceName);
        Assert.Equal("NorthwindShipping", identity.ServiceNameFor("default"));
        Assert.Equal("NorthwindShipping-east", identity.ServiceNameFor("east"));
        Assert.Equal("NWSHIP", identity.EnvPrefix);
        Assert.Equal("NWSHIP_INSTANCE", identity.InstanceVariable);
        Assert.Equal("/var/lib/northwind-shipping", identity.LinuxInstancesRoot);
        Assert.EndsWith(Path.Combine("Northwind.Shipping", "instances"), identity.WindowsInstancesRoot);
        Assert.Throws<ArgumentException>(() => WorkerAppIdentity.NormalizeEnvPrefix("CC-OR"));
        Assert.Throws<ArgumentException>(() =>
            WorkerAppIdentity.Create(typeof(WorkerHostBuilder).Assembly, envPrefix: "CC-OR"));
    }

    [Fact]
    public void Instance_locator_reads_the_identity_prefix_and_root()
    {
        var identity = WorkerAppIdentity.Create(
            typeof(WorkerHostBuilder).Assembly,
            name: "Northwind.Shipping",
            envPrefix: "NWSHIP");
        var directory = Path.Combine(Path.GetTempPath(), "nwship-instance-dir");
        Directory.CreateDirectory(directory);

        var fromName = InstanceLocator.Resolve(
            [],
            name => name == identity.InstanceVariable ? "billing" : null,
            "/opt/app",
            identity);
        var fromDirectory = InstanceLocator.Resolve(
            [],
            name => name == identity.InstanceDirectoryVariable ? directory : null,
            "/opt/app",
            identity);

        Assert.Equal("billing", fromName.Name);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(identity.DefaultInstancesRoot, "billing")),
            fromName.Directory);
        Assert.Equal(Path.GetFullPath(directory), fromDirectory.Directory);
    }

    [Fact]
    public void Host_uses_the_explicit_identity_for_the_application_name_and_instance_dir()
    {
        var instanceDir = Path.Combine(Path.GetTempPath(), "acme-instance");
        var install = Path.Combine(Path.GetTempPath(), "acme-install");
        Directory.CreateDirectory(instanceDir);
        Directory.CreateDirectory(install);
        var identity = WorkerAppIdentity.Create(
            typeof(WorkerHostBuilder).Assembly,
            name: "Acme",
            envPrefix: "acme");
        var previous = Environment.GetEnvironmentVariable(identity.InstanceDirectoryVariable);
        Environment.SetEnvironmentVariable(identity.InstanceDirectoryVariable, instanceDir);
        try
        {
            using var host = WorkerHostBuilder.Create(
                [],
                contentRoot: install,
                environmentName: "Production",
                identity: identity).Build();

            Assert.Equal("Acme", host.Services.GetRequiredService<IHostEnvironment>().ApplicationName);
            Assert.Equal(
                Path.GetFullPath(instanceDir),
                host.Services.GetRequiredService<IInstanceContext>().InstanceDirectory);
            Assert.Equal("ACME", host.Services.GetRequiredService<WorkerAppIdentity>().EnvPrefix);
        }
        finally
        {
            Environment.SetEnvironmentVariable(identity.InstanceDirectoryVariable, previous);
        }
    }

    [Fact]
    public void Check_config_prints_registered_sections_and_masks_secrets()
    {
        var output = new RecordingOutput();
        const string connectionString = "Server=secret-value";
        const string token = "super-secret-token";
        const string probe = "probe-marker-9f3c";
        using var host = WorkerHostBuilder.Create(
            [],
            contentRoot: RepoPaths.HostProject(),
            environmentName: "Production",
            configure: builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Data:ConnectionString"] = connectionString,
                    ["Client:ApiToken"] = token,
                    ["Probe"] = probe,
                });
                builder.Services.AddSingleton<ICommandOutput>(output);
            }).Build();

        var code = host.Services.GetRequiredService<CheckConfigHandler>().Execute();

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(
            output.Lines,
            line => line.StartsWith("Data:ConnectionString = ", StringComparison.Ordinal)
                && line.Contains("****", StringComparison.Ordinal));
        Assert.DoesNotContain(output.Lines, line => line.Contains(connectionString, StringComparison.Ordinal));
        Assert.DoesNotContain(output.Lines, line => line.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain(output.Lines, line => line.Contains(probe, StringComparison.Ordinal));
        Assert.Contains(output.Lines, line => line == "Telemetry version: 0.1.1");
    }

    [Fact]
    public void Service_version_comes_from_the_application_identity()
    {
        var application = WorkerAppIdentity.FromAssembly(typeof(WorkerHostBuilder).Assembly);
        var telemetry = TelemetryIdentity.Create(
            new ConfigurationBuilder().Build(),
            application,
            "Production",
            new IdentityProbeInstance());

        Assert.Equal(application.Name, telemetry.ServiceName);
        Assert.Equal(application.Version, telemetry.ServiceVersion);
        Assert.Equal(TelemetryIdentity.ReadServiceVersion(), telemetry.LibraryVersion);
        Assert.Equal(telemetry.LibraryVersion, telemetry.ToSerilogAttributes()["workerkit.version"]);
        Assert.Equal("0.1.1", telemetry.ToSerilogAttributes()["service.version"]);
    }

    private sealed class IdentityProbeInstance : IInstanceContext
    {
        public string InstanceName => "default";

        public string InstallDirectory => "/opt/app";

        public string InstanceDirectory => "/opt/app";

        public string LogDirectory => "/opt/app/logs";

        public string DataDirectory => "/opt/app/data";

        public string TempDirectory => "/opt/app/temp";

        public string ResolvePath(string path) => path;
    }
}
