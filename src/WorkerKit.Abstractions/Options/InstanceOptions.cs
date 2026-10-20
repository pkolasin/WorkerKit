namespace WorkerKit.Abstractions.Options;

/// <summary>Directories and process lock for one instance.</summary>
public sealed class InstanceOptions
{
    /// <summary>Configuration section name. The value is <c>Instance</c>.</summary>
    public const string SectionName = "Instance";

    /// <summary>
    /// Directory for downloaded batches and other instance data.
    /// Relative paths resolve against the instance directory.
    /// </summary>
    /// <remarks>The initializer is <c>data</c>.</remarks>
    public string DataDirectory { get; set; } = "data";

    /// <summary>
    /// Scratch directory. Relative paths resolve against the instance directory.
    /// </summary>
    /// <remarks>The initializer is <c>temp</c>.</remarks>
    public string TempDirectory { get; set; } = "temp";

    /// <summary>Delete the contents of <see cref="TempDirectory"/> when the host starts.</summary>
    /// <remarks>The initializer is <see langword="true"/>.</remarks>
    public bool CleanTempOnStart { get; set; } = true;

    /// <summary>
    /// When true, <c>run</c> holds an exclusive lock in the instance directory so a second process cannot start.
    /// Connector commands and <c>check-config</c> never take the lock.
    /// </summary>
    /// <remarks>The initializer is <see langword="true"/>.</remarks>
    public bool RequireLock { get; set; } = true;
}
