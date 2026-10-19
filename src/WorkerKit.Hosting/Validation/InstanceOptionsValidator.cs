
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

public sealed class InstanceOptionsValidator : IValidateOptions<InstanceOptions>
{
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
