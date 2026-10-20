
using WorkerKit.Abstractions.Instances;
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting;

/// <summary>
/// Instance directories for one process. Paths are fully qualified when <see cref="Create"/> returns.
/// </summary>
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

    /// <summary>Instance name from <see cref="InstanceLocation.Name"/>.</summary>
    public string InstanceName { get; }

    /// <summary>Install directory. This is also the host content root.</summary>
    public string InstallDirectory { get; }

    /// <summary>Directory that holds this instance's configuration, logs, and data.</summary>
    public string InstanceDirectory { get; }

    /// <summary>Directory of the resolved log file. This is the instance directory when the log path has no directory.</summary>
    public string LogDirectory { get; }

    /// <summary>Data directory. A relative <c>Instance:DataDirectory</c> is resolved against <see cref="InstanceDirectory"/>.</summary>
    public string DataDirectory { get; }

    /// <summary>Temp directory. A relative <c>Instance:TempDirectory</c> is resolved against <see cref="InstanceDirectory"/>.</summary>
    public string TempDirectory { get; }

    /// <summary>
    /// Builds directories from <paramref name="location"/> and the instance and log-file configuration.
    /// </summary>
    /// <param name="location">Resolved instance name and directory.</param>
    /// <param name="installDirectory">Install directory, stored as a full path.</param>
    /// <param name="configuration">Configuration read for the log, data, and temp paths and for cleaning the temp directory on start.</param>
    /// <returns>An environment whose directories are full paths.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> or <paramref name="configuration"/> is null.</exception>
    /// <remarks>
    /// A missing <c>LogFile:Path</c> falls back to <c>logs/worker-.json</c>, <c>Instance:DataDirectory</c> to <c>data</c>,
    /// <c>Instance:TempDirectory</c> to <c>temp</c>, and <c>Instance:CleanTempOnStart</c> to <see langword="true"/>.
    /// </remarks>
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

    /// <summary>
    /// Resolves <paramref name="path"/> against <see cref="InstanceDirectory"/> when it is relative.
    /// An absolute path is returned as a full path.
    /// </summary>
    /// <param name="path">Relative or absolute path.</param>
    /// <returns>The full path.</returns>
    public string ResolvePath(string path) => ResolveAgainst(InstanceDirectory, path);

    /// <summary>
    /// Creates the log, data, and temp directories and checks that each is writable.
    /// When temp cleaning is enabled, deletes the entries in the temp directory first.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Temp cleaning is enabled and the temp directory is, or contains, the instance directory, the install directory, or the data directory.
    /// </exception>
    /// <remarks>Creating a log, data, or temp directory that cannot be written also fails with <see cref="InvalidOperationException"/>.</remarks>
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
