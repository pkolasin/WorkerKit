
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

/// <summary>Checks that <see cref="DataOptions.Provider"/> is <c>None</c> or <c>Dapper</c>, and that Dapper has a connection string.</summary>
public sealed class DataOptionsValidator : IValidateOptions<DataOptions>
{
    /// <summary>
    /// Accepts <c>None</c> and <c>Dapper</c>. <c>Dapper</c> requires <see cref="DataOptions.ConnectionString"/>.
    /// </summary>
    /// <param name="name">Named-options name. This validator does not use it.</param>
    /// <param name="options">Options to check.</param>
    /// <returns>Success, or a failure that names <c>Data:Provider</c> or <c>Data:ConnectionString</c>.</returns>
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
