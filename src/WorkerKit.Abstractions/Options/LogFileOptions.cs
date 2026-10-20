using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

/// <summary>Rolling log file for one instance.</summary>
public sealed class LogFileOptions
{
    /// <summary>Configuration section name. The value is <c>LogFile</c>.</summary>
    public const string SectionName = "LogFile";

    /// <summary>
    /// Rolling file path. Relative paths are resolved against the instance directory, not the content root or the process working directory.
    /// </summary>
    /// <remarks>The initializer is <c>logs/worker-.json</c>. <see cref="RequiredAttribute"/> marks the value required.</remarks>
    [Required(ErrorMessage = "LogFile:Path is required.")]
    public string Path { get; set; } = "logs/worker-.json";

    /// <summary>Number of rolling log files to retain.</summary>
    /// <remarks>The initializer is <c>14</c>. <see cref="RangeAttribute"/> allows 1 through 365.</remarks>
    [Range(1, 365, ErrorMessage = "LogFile:RetainedFileCount must be between 1 and 365.")]
    public int RetainedFileCount { get; set; } = 14;

    /// <summary>How often the log file is flushed, in seconds.</summary>
    /// <remarks>The initializer is <c>1</c>. <see cref="RangeAttribute"/> allows 1 through 3600.</remarks>
    [Range(1, 3_600, ErrorMessage = "LogFile:FlushIntervalSeconds must be between 1 and 3600.")]
    public int FlushIntervalSeconds { get; set; } = 1;
}
