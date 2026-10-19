
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

public sealed class DataOptionsValidator : IValidateOptions<DataOptions>
{
    public ValidateOptionsResult Validate(string? name, DataOptions options)
    {
        var provider = options.Provider?.Trim() ?? "";
        if (!provider.Equals("None", StringComparison.OrdinalIgnoreCase)
            && !provider.Equals("Dapper", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("Data:Provider must be None or Dapper.");
        }

        if (provider.Equals("Dapper", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Data:ConnectionString is required when Data:Provider is Dapper.");
        }

        return ValidateOptionsResult.Success;
    }
}
