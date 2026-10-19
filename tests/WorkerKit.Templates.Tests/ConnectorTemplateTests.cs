using System.Diagnostics;
using System.Text.Json;
using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Templates.Tests;

public sealed class TemplateFeedFixture : IDisposable
{
    public TemplateFeedFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "wk-template-" + Guid.NewGuid().ToString("N"));
        Feed = Path.Combine(Root, "feed");
        Hive = Path.Combine(Root, "hive");
        Packages = Path.Combine(Root, "packages");
        Directory.CreateDirectory(Feed);
        Directory.CreateDirectory(Packages);
        // 0.1.1-dev is reused across runs. The global NuGet cache would keep the first nupkg with that version.
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", Packages);
        var repo = RepoRoot();
        var version = "0.1.1-dev";
        foreach (var project in new[]
        {
            "src/WorkerKit.Abstractions/WorkerKit.Abstractions.csproj",
            "src/WorkerKit.Hosting/WorkerKit.Hosting.csproj",
            "src/WorkerKit.Soap/WorkerKit.Soap.csproj",
            "src/WorkerKit.Testing/WorkerKit.Testing.csproj",
            "src/WorkerKit.Templates/WorkerKit.Templates.csproj",
        })
        {
            var packed = TemplateCommand.Run(
                "dotnet",
                repo,
                ["pack", Path.Combine(repo, project), "-c", "Release", "-o", Feed, "-p:Version=" + version, "--nologo", "-warnaserror"]);
            if (packed.ExitCode != 0)
            {
                throw new InvalidOperationException("dotnet pack failed for " + project + Environment.NewLine + packed.Output);
            }
        }

        var package = Directory.GetFiles(Feed, "WorkerKit.Templates." + version + ".nupkg").Single();
        var installed = TemplateCommand.Run(
            "dotnet",
            repo,
            ["new", "install", package, "--debug:custom-hive", Hive]);
        if (installed.ExitCode != 0)
        {
            throw new InvalidOperationException(installed.Output);
        }
    }

    public string Root { get; }

    public string Feed { get; }

    public string Hive { get; }

    public string Packages { get; }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorkerKit.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find WorkerKit.sln.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}

public sealed class ConnectorTemplateTests : IClassFixture<TemplateFeedFixture>
{
    private readonly TemplateFeedFixture _feed;

    public ConnectorTemplateTests(TemplateFeedFixture feed)
    {
        _feed = feed;
    }

    [Fact]
    public void Default_connector_builds_tests_and_publishes_its_name()
    {
        var output = Path.Combine(_feed.Root, "Contoso.Billing");
        var created = NewConnector(output, ["-n", "Contoso.Billing"]);
        Assert.True(created.ExitCode == 0, created.Output);
        Assert.Contains("--env-prefix", Help().Output, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(output, "src", "Contoso.Billing.Host", "Program.cs"));
        Assert.Contains("WorkerApp.CreateBuilder<Program>", program, StringComparison.Ordinal);
        Assert.Contains("app.Commands.Add<SampleCommand>", program, StringComparison.Ordinal);
        Assert.Contains("AddContosoBillingConnector", program, StringComparison.Ordinal);
        Assert.Contains("CONTOSOBILLING", program, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(output, "src", "Contoso.Billing.Host", "Queue")));
        Assert.Contains("sdk:10.0", File.ReadAllText(Path.Combine(output, "Dockerfile")), StringComparison.Ordinal);
        Assert.Contains("CONTOSOBILLING_INSTANCE_DIR", File.ReadAllText(Path.Combine(output, "Dockerfile")), StringComparison.Ordinal);
        Assert.Contains(">net10.0<", File.ReadAllText(Path.Combine(output, "Directory.Build.props")), StringComparison.Ordinal);
        Assert.Contains("0.1.1-dev", File.ReadAllText(Path.Combine(output, "Directory.Packages.props")), StringComparison.Ordinal);
        Assert.DoesNotContain("//#if", File.ReadAllText(Path.Combine(output, "src", "Contoso.Billing.Host", "appsettings.json")), StringComparison.Ordinal);
        using (var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "src", "Contoso.Billing.Host", "appsettings.json"))))
        {
            Assert.False(settings.RootElement.TryGetProperty("Queue", out _));
            Assert.False(settings.RootElement.GetProperty("Sample").GetProperty("Enabled").GetBoolean());
        }

        UseFeed(output);
        AssertCommand(TemplateCommand.Run("dotnet", output, ["build", "Contoso.Billing.sln", "--nologo", "-warnaserror"]));
        AssertCommand(TemplateCommand.Run("dotnet", output, ["test", "Contoso.Billing.sln", "--nologo", "-warnaserror", "--no-build"]));

        var publishDir = Path.Combine(output, "artifacts", "publish");
        AssertCommand(TemplateCommand.Run(
            "dotnet",
            output,
            ["publish", Path.Combine(output, "src", "Contoso.Billing.Host", "Contoso.Billing.Host.csproj"), "-c", "Release", "-o", publishDir, "--nologo", "-warnaserror"]));
        Assert.True(File.Exists(Path.Combine(publishDir, "deploy", "windows", "install-service.ps1")));
        Assert.True(File.Exists(Path.Combine(publishDir, "deploy", "systemd", "install.sh")));
        Assert.True(File.Exists(Path.Combine(publishDir, "appsettings.instance.template.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(publishDir, "workerkit.app.json")));
        Assert.Equal("Contoso.Billing", manifest.RootElement.GetProperty("name").GetString());
        Assert.Equal("CONTOSOBILLING", manifest.RootElement.GetProperty("envPrefix").GetString());
        Assert.Equal("Contoso.Billing.Host.exe", manifest.RootElement.GetProperty("windowsExecutable").GetString());
        Assert.Equal("Contoso.Billing.Host.dll", manifest.RootElement.GetProperty("assemblyFile").GetString());

        DeleteBuildOutput(output);
        AssertCommand(TemplateCommand.Run(
            "dotnet",
            output,
            ["build", "Contoso.Billing.sln", "--nologo", "-warnaserror", "-p:UseLocalWorkerKit=" + TemplateFeedFixture.RepoRoot()]));
    }

    [Fact]
    public void Queue_and_net8_options_are_applied()
    {
        var output = Path.Combine(_feed.Root, "Northwind.Shipping");
        var created = NewConnector(
            output,
            ["-n", "Northwind.Shipping", "--queue", "--framework", "net8.0", "--env-prefix", "NORTHWIND"]);
        Assert.True(created.ExitCode == 0, created.Output);
        Assert.True(File.Exists(Path.Combine(output, "src", "Northwind.Shipping.Host", "Queue", "SampleQueue.cs")));
        var program = File.ReadAllText(Path.Combine(output, "src", "Northwind.Shipping.Host", "Program.cs"));
        Assert.Contains("AddNorthwindShippingConnector", program, StringComparison.Ordinal);
        Assert.Contains("NORTHWIND", program, StringComparison.Ordinal);
        Assert.Contains(">net8.0<", File.ReadAllText(Path.Combine(output, "Directory.Build.props")), StringComparison.Ordinal);
        Assert.Contains("sdk:8.0", File.ReadAllText(Path.Combine(output, "Dockerfile")), StringComparison.Ordinal);
        Assert.Contains("NORTHWIND_INSTANCE_DIR", File.ReadAllText(Path.Combine(output, "Dockerfile")), StringComparison.Ordinal);
        using (var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "src", "Northwind.Shipping.Host", "appsettings.json"))))
        {
            Assert.False(settings.RootElement.GetProperty("Queue").GetProperty("sample-queue").GetProperty("Enabled").GetBoolean());
        }

        UseFeed(output);
        AssertCommand(TemplateCommand.Run("dotnet", output, ["build", "Northwind.Shipping.sln", "--nologo", "-warnaserror"]));
        AssertCommand(TemplateCommand.Run("dotnet", output, ["test", "Northwind.Shipping.sln", "--nologo", "-warnaserror", "--no-build"]));
    }

    [Fact]
    public void Invalid_names_are_rejected()
    {
        foreach (var name in new[] { "my-connector", "9Lives" })
        {
            var output = Path.Combine(_feed.Root, "invalid-" + name);
            var created = NewConnector(output, ["-n", name]);
            Assert.NotEqual(0, created.ExitCode);
            Assert.Contains("Connector name '" + name + "' is not allowed", created.Output, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(output, "src", name + ".Host", "Program.cs")));
        }
    }

    [Theory]
    [InlineData("Contoso.Billing")]
    [InlineData("Northwind.Shipping")]
    [InlineData("HTTPServer.Sync")]
    public void Cli_reference_kebab_matches_runtime(string name)
    {
        var output = Path.Combine(_feed.Root, "kebab-" + name);
        var created = NewConnector(output, ["-n", name]);
        Assert.Equal(0, created.ExitCode);
        var cli = File.ReadAllText(Path.Combine(output, "doc", "cli.md"));
        Assert.Contains("/var/lib/" + WorkerAppIdentity.ToKebabCase(name), cli, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid-name.csproj", Directory.GetFiles(output).Select(Path.GetFileName));
    }

    private (int ExitCode, string Output) NewConnector(string output, IEnumerable<string> arguments)
    {
        var args = new List<string> { "new", "workerkit-connector", "--debug:custom-hive", _feed.Hive, "-o", output };
        args.AddRange(arguments);
        return TemplateCommand.Run("dotnet", TemplateFeedFixture.RepoRoot(), args);
    }

    private (int ExitCode, string Output) Help() =>
        TemplateCommand.Run("dotnet", TemplateFeedFixture.RepoRoot(), ["new", "workerkit-connector", "-h", "--debug:custom-hive", _feed.Hive]);

    private void UseFeed(string output)
    {
        File.WriteAllText(
            Path.Combine(output, "nuget.config"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="FEED" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
              </packageSources>
            </configuration>
            """.Replace("FEED", _feed.Feed, StringComparison.Ordinal));
    }

    private static void DeleteBuildOutput(string output)
    {
        foreach (var name in new[] { "bin", "obj" })
        {
            foreach (var directory in Directory.GetDirectories(output, name, SearchOption.AllDirectories))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void AssertCommand((int ExitCode, string Output) result) =>
        Assert.True(result.ExitCode == 0, result.Output);
}

internal static class TemplateCommand
{
    public static (int ExitCode, string Output) Run(string fileName, string workingDirectory, IEnumerable<string> arguments)
    {
        var start = new ProcessStartInfo(fileName, string.Join(" ", arguments.Select(Quote)))
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_HOME"] = Path.Combine(Path.GetTempPath(), "wk-cli-home");
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        // dotnet test exports the host SDK location. A generated global.json that selects
        // the net8 SDK then loads those net10 tasks and fails to resolve System.Runtime.
        start.Environment.Remove("MSBuildSDKsPath");
        start.Environment.Remove("MSBuildExtensionsPath");
        start.Environment.Remove("MSBuildLoadMicrosoftTargetsReadOnly");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + fileName);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(10)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(fileName + " did not exit within 10 minutes.");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static string Quote(string value) => value.Contains(' ', StringComparison.Ordinal) ? "\"" + value + "\"" : value;
}
