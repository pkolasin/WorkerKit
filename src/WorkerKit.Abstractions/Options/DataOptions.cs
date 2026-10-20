namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Optional persistence. <see cref="Provider"/> <c>None</c> skips the database probe.
/// <c>Dapper</c> requires the connector to register <c>IDbConnectionFactory</c>.
/// </summary>
public sealed class DataOptions
{
    /// <summary>Configuration section name. The value is <c>Data</c>.</summary>
    public const string SectionName = "Data";

    /// <summary>Persistence provider name.</summary>
    /// <remarks>The initializer is <c>None</c>. <c>None</c> skips the database probe. <c>Dapper</c> requires the connector to register <see cref="WorkerKit.Abstractions.Data.IDbConnectionFactory"/>.</remarks>
    public string Provider { get; set; } = "None";

    /// <summary>Connection string for <see cref="Provider"/>.</summary>
    /// <remarks>The initializer is <c>""</c>.</remarks>
    public string ConnectionString { get; set; } = "";
}
