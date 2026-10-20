using System.Diagnostics;
using System.IO.Compression;

namespace WorkerKit.Hosting.Tests;

/// <summary>
/// Packs the four library projects and checks that each nupkg contains the compiler XML file for both target frameworks.
/// </summary>
public sealed class DocumentationPackageTests
{
#if NET10_0
    [Fact]
    public void Library_packages_contain_xml_documentation_for_both_target_frameworks()
    {
        var root = Path.Combine(Path.GetTempPath(), "wk-docpack-" + Guid.NewGuid().ToString("N"));
        var feed = Path.Combine(root, "feed");
        var packages = Path.Combine(root, "packages");
        Directory.CreateDirectory(feed);
        try
        {
            var repo = RepoPaths.RepoFile("WorkerKit.sln");
            repo = Path.GetDirectoryName(repo)!;
            var version = "0.1.1-dev.docxml";
            foreach (var project in new[]
            {
                "src/WorkerKit.Abstractions/WorkerKit.Abstractions.csproj",
                "src/WorkerKit.Hosting/WorkerKit.Hosting.csproj",
                "src/WorkerKit.Soap/WorkerKit.Soap.csproj",
                "src/WorkerKit.Testing/WorkerKit.Testing.csproj",
            })
            {
                var packed = Run(
                    repo,
                    packages,
                    ["pack", Path.Combine(repo, project), "-c", "Release", "-o", feed, "-p:Version=" + version, "--nologo", "-warnaserror"]);
                Assert.True(packed.ExitCode == 0, packed.Output);
            }

            foreach (var assembly in new[]
            {
                "WorkerKit.Abstractions",
                "WorkerKit.Hosting",
                "WorkerKit.Soap",
                "WorkerKit.Testing",
            })
            {
                var nupkg = Path.Combine(feed, assembly + "." + version + ".nupkg");
                Assert.True(File.Exists(nupkg), "Missing " + nupkg);
                using var zip = ZipFile.OpenRead(nupkg);
                foreach (var tfm in new[] { "net8.0", "net10.0" })
                {
                    var entryName = "lib/" + tfm + "/" + assembly + ".xml";
                    var entry = zip.GetEntry(entryName);
                    Assert.True(entry is not null, nupkg + " does not contain " + entryName);
                    using var reader = new StreamReader(entry.Open());
                    var xml = reader.ReadToEnd();
                    Assert.Contains("<summary>", xml, StringComparison.Ordinal);
                    Assert.Contains(assembly, xml, StringComparison.Ordinal);
                }
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
#endif

    private static (int ExitCode, string Output) Run(string repo, string packages, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("dotnet", string.Join(" ", arguments.Select(Quote)))
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["NUGET_PACKAGES"] = packages;
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment.Remove("MSBuildSDKsPath");
        start.Environment.Remove("MSBuildExtensionsPath");
        start.Environment.Remove("MSBuildLoadMicrosoftTargetsReadOnly");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(10)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("dotnet pack did not exit within 10 minutes.");
        }

        return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static string Quote(string value) => value.Contains(' ', StringComparison.Ordinal) ? "\"" + value + "\"" : value;
}
