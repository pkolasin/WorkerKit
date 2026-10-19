
using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting;

public sealed class InstanceEnvironment : IInstanceContext
{
    private readonly bool _cleanTempOnStart;

    private InstanceEnvironment(
        string instanceName,
        string installDirectory,
        string instanceDirectory,
        string logDirectory,
        string dataDirectory,
        string tempDirectory,
        bool cleanTempOnStart)
    {
        InstanceName = instanceName;
        InstallDirectory = installDirectory;
        InstanceDirectory = instanceDirectory;
        LogDirectory = logDirectory;
        DataDirectory = dataDirectory;
        TempDirectory = tempDirectory;
        _cleanTempOnStart = cleanTempOnStart;
    }

    public string InstanceName { get; }

    public string InstallDirectory { get; }

    public string InstanceDirectory { get; }

    public string LogDirectory { get; }

    public string DataDirectory { get; }

    public string TempDirectory { get; }

    public static InstanceEnvironment Create(InstanceLocation location, string installDirectory, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(configuration);

        var instanceDirectory = Path.GetFullPath(location.Directory);
        var logPath = configuration.GetValue($"{LogFileOptions.SectionName}:Path", "logs/worker-.json") ?? "logs/worker-.json";
        var dataPath = configuration.GetValue($"{InstanceOptions.SectionName}:DataDirectory", "data") ?? "data";
        var tempPath = configuration.GetValue($"{InstanceOptions.SectionName}:TempDirectory", "temp") ?? "temp";
        var resolvedLog = ResolveAgainst(instanceDirectory, logPath);
        var logDirectory = Path.GetDirectoryName(resolvedLog);
        if (string.IsNullOrEmpty(logDirectory))
        {
            logDirectory = instanceDirectory;
        }

        return new InstanceEnvironment(
            location.Name,
            Path.GetFullPath(installDirectory),
            instanceDirectory,
            logDirectory,
            ResolveAgainst(instanceDirectory, dataPath),
            ResolveAgainst(instanceDirectory, tempPath),
            configuration.GetValue($"{InstanceOptions.SectionName}:CleanTempOnStart", true));
    }

    public string ResolvePath(string path) => ResolveAgainst(InstanceDirectory, path);

    public void EnsureDirectories()
    {
        if (_cleanTempOnStart)
        {
            if (ContainsPath(TempDirectory, InstanceDirectory)
                || ContainsPath(TempDirectory, InstallDirectory)
                || ContainsPath(TempDirectory, DataDirectory))
            {
                throw new InvalidOperationException(
                    $"Refusing to clean Instance:TempDirectory '{TempDirectory}' because it is, or contains, the instance directory, the install directory, or Instance:DataDirectory.");
            }

            if (Directory.Exists(TempDirectory))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(TempDirectory))
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, recursive: true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
            }
        }

        CreateWritable(LogDirectory);
        CreateWritable(DataDirectory);
        CreateWritable(TempDirectory);
    }

    internal static string ResolveAgainst(string instanceDirectory, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(instanceDirectory, path));
    }

    private static void CreateWritable(string directory)
    {
        var probe = Path.Combine(directory, ".write-test");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Cannot create or write '{directory}'.", exception);
        }
    }

    /// <summary>True when <paramref name="parent"/> is the same path as <paramref name="child"/> or a directory that contains it.</summary>
    private static bool ContainsPath(string parent, string child)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var parentFull = Path.GetFullPath(parent);
        var childFull = Path.GetFullPath(child);
        var parentRoot = Path.GetPathRoot(parentFull);
        if (parentRoot is not null && parentFull.Equals(parentRoot, comparison))
        {
            var childRoot = Path.GetPathRoot(childFull);
            return childRoot is not null && childRoot.Equals(parentRoot, comparison);
        }

        parentFull = parentFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        childFull = childFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return childFull.Equals(parentFull, comparison)
            || childFull.StartsWith(parentFull + Path.DirectorySeparatorChar, comparison);
    }
}
