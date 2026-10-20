
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

/// <summary>Checks that the data and temp directories are present, legal paths, and different.</summary>
public sealed class InstanceOptionsValidator : IValidateOptions<InstanceOptions>
{
    /// <summary>
    /// Requires <see cref="InstanceOptions.DataDirectory"/> and <see cref="InstanceOptions.TempDirectory"/>,
    /// rejects invalid path characters, and rejects equal paths.
    /// Path comparison is case-insensitive on Windows and ordinal otherwise.
    /// </summary>
    /// <param name="name">Named-options name. This validator does not use it.</param>
    /// <param name="options">Options to check.</param>
    /// <returns>Success, or the collected failures.</returns>
    public ValidateOptionsResult Validate(string? name, InstanceOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.DataDirectory))
        {
            failures.Add("Instance:DataDirectory is required.");
        }
        else if (options.DataDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            failures.Add("Instance:DataDirectory contains invalid path characters.");
        }

        if (string.IsNullOrWhiteSpace(options.TempDirectory))
        {
            failures.Add("Instance:TempDirectory is required.");
        }
        else if (options.TempDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            failures.Add("Instance:TempDirectory contains invalid path characters.");
        }

        if (!string.IsNullOrWhiteSpace(options.DataDirectory)
            && !string.IsNullOrWhiteSpace(options.TempDirectory)
            && string.Equals(options.DataDirectory.Trim(), options.TempDirectory.Trim(), PathComparison))
        {
            failures.Add("Instance:DataDirectory and Instance:TempDirectory must be different.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
