using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

public sealed class LogFileOptions
{
    public const string SectionName = "LogFile";

    /// <summary>
    /// Rolling file path. Relative paths are resolved against the instance directory, not the content root or the process working directory.
    /// </summary>
    [Required(ErrorMessage = "LogFile:Path is required.")]
    public string Path { get; set; } = "logs/worker-.json";

    [Range(1, 365, ErrorMessage = "LogFile:RetainedFileCount must be between 1 and 365.")]
    public int RetainedFileCount { get; set; } = 14;

    [Range(1, 3_600, ErrorMessage = "LogFile:FlushIntervalSeconds must be between 1 and 3600.")]
    public int FlushIntervalSeconds { get; set; } = 1;
}
