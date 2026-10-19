namespace WorkerKit.Abstractions.Options;

public sealed class InstanceOptions
{
    public const string SectionName = "Instance";

    /// <summary>
    /// Directory for downloaded batches and other instance data.
    /// Relative paths resolve against the instance directory.
    /// </summary>
    public string DataDirectory { get; set; } = "data";

    /// <summary>
    /// Scratch directory. Relative paths resolve against the instance directory.
    /// </summary>
    public string TempDirectory { get; set; } = "temp";

    /// <summary>Delete the contents of <see cref="TempDirectory"/> when the host starts.</summary>
    public bool CleanTempOnStart { get; set; } = true;

    /// <summary>
    /// When true, <c>run</c> holds an exclusive lock in the instance directory so a second process cannot start.
    /// Connector commands and <c>check-config</c> never take the lock.
    /// </summary>
    public bool RequireLock { get; set; } = true;
}
