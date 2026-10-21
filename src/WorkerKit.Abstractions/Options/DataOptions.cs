namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Optional persistence. <see cref="Provider"/> <c>None</c> skips the database probe.
/// <c>Dapper</c> requires the connector to register <c>IDbConnectionFactory</c>.
/// <c>SqlServer</c> and <c>PostgreSQL</c> are the providers read by WorkerKit.Sql when its services are resolved.
/// </summary>
public sealed class DataOptions
{
    /// <summary>Configuration section name. The value is <c>Data</c>.</summary>
    public const string SectionName = "Data";

    /// <summary>Provider value that skips the database probe. The value is <c>None</c>.</summary>
    public const string NoneProvider = "None";

    /// <summary>Provider value for a connector-supplied <c>IDbConnectionFactory</c>. The value is <c>Dapper</c>.</summary>
    public const string DapperProvider = "Dapper";

    /// <summary>Provider value for SQL Server. The value is <c>SqlServer</c>.</summary>
    public const string SqlServerProvider = "SqlServer";

    /// <summary>Provider value for PostgreSQL. The value is <c>PostgreSQL</c>.</summary>
    public const string PostgreSqlProvider = "PostgreSQL";

    /// <summary>Persistence provider name.</summary>
    /// <remarks>The initializer is <c>None</c>. <c>None</c> skips the database probe. <c>Dapper</c> requires the connector to register <see cref="WorkerKit.Abstractions.Data.IDbConnectionFactory"/>. <c>SqlServer</c> and <c>PostgreSQL</c> are accepted for WorkerKit.Sql.</remarks>
    public string Provider { get; set; } = "None";

    /// <summary>Connection string for <see cref="Provider"/>.</summary>
    /// <remarks>The initializer is <c>""</c>.</remarks>
    public string ConnectionString { get; set; } = "";
}
