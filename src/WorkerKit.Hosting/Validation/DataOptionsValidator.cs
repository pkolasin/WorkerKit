
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Validation;

/// <summary>
/// Checks that <see cref="DataOptions.Provider"/> is <c>None</c>, <c>Dapper</c>, <c>SqlServer</c>, or <c>PostgreSQL</c>,
/// and that every provider except <c>None</c> has a connection string.
/// </summary>
public sealed class DataOptionsValidator : IValidateOptions<DataOptions>
{
    /// <summary>
    /// Accepts <c>None</c>, <c>Dapper</c>, <c>SqlServer</c>, <c>PostgreSQL</c>, and the <c>Postgres</c> alias.
    /// Every provider except <c>None</c> requires <see cref="DataOptions.ConnectionString"/>.
    /// </summary>
    /// <param name="name">Named-options name. This validator does not use it.</param>
    /// <param name="options">Options to check.</param>
    /// <returns>Success, or a failure that names <c>Data:Provider</c> or <c>Data:ConnectionString</c>.</returns>
    public ValidateOptionsResult Validate(string? name, DataOptions options)
    {
        var provider = options.Provider?.Trim() ?? "";
        if (!IsKnown(provider))
        {
            return ValidateOptionsResult.Fail("Data:Provider must be None, Dapper, SqlServer, or PostgreSQL.");
        }

        if (!provider.Equals(DataOptions.NoneProvider, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Data:ConnectionString is required when Data:Provider is not None.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsKnown(string provider) =>
        provider.Equals(DataOptions.NoneProvider, StringComparison.OrdinalIgnoreCase)
        || provider.Equals(DataOptions.DapperProvider, StringComparison.OrdinalIgnoreCase)
        || provider.Equals(DataOptions.SqlServerProvider, StringComparison.OrdinalIgnoreCase)
        || provider.Equals(DataOptions.PostgreSqlProvider, StringComparison.OrdinalIgnoreCase)
        || provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase);
}
