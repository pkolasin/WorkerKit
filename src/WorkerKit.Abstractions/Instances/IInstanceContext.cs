namespace WorkerKit.Abstractions.Instances;

/// <summary>
/// The install directory (content root) and the directory that holds this process's configuration, logs, and data.
/// </summary>
public interface IInstanceContext
{
    /// <summary>Name of this instance.</summary>
    string InstanceName { get; }

    /// <summary>Install directory. This is also the host content root.</summary>
    string InstallDirectory { get; }

    /// <summary>Directory that holds this process's configuration, logs, and data.</summary>
    string InstanceDirectory { get; }

    /// <summary>Directory for this instance's logs.</summary>
    string LogDirectory { get; }

    /// <summary>Directory for this instance's data.</summary>
    string DataDirectory { get; }

    /// <summary>Directory for this instance's temporary files.</summary>
    string TempDirectory { get; }

    /// <summary>
    /// Resolves <paramref name="path"/> against <see cref="InstanceDirectory"/> when it is relative.
    /// Absolute paths are returned unchanged (normalized).
    /// </summary>
    /// <param name="path">Path to resolve.</param>
    /// <returns>
    /// <paramref name="path"/> resolved against <see cref="InstanceDirectory"/> when it is relative;
    /// the normalized absolute path when it is absolute.
    /// </returns>
    string ResolvePath(string path);
}
