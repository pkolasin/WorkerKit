namespace WorkerKit.Hosting.Tests;

internal static class RepoPaths
{
    public static string HostProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ExampleHost");
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/ExampleHost from " + AppContext.BaseDirectory);
    }

    public static string RepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WorkerKit.sln")))
            {
                return Path.Combine(dir.FullName, relativePath);
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate WorkerKit.sln from " + AppContext.BaseDirectory);
    }
}
