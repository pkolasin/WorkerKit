namespace WorkerKit.Abstractions.Instances;

/// <summary>
/// The install directory (content root) and the directory that holds this process's configuration, logs, and data.
/// </summary>
public interface IInstanceContext
{
    string InstanceName { get; }

    /// <summary>Install directory. This is also the host content root.</summary>
    string InstallDirectory { get; }

    string InstanceDirectory { get; }

    string LogDirectory { get; }

    string DataDirectory { get; }

    string TempDirectory { get; }

    /// <summary>
    /// Resolves <paramref name="path"/> against <see cref="InstanceDirectory"/> when it is relative.
    /// Absolute paths are returned unchanged (normalized).
    /// </summary>
    string ResolvePath(string path);
}
