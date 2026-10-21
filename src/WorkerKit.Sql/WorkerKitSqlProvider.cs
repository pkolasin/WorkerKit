namespace WorkerKit.Sql;

/// <summary>Database provider selected by <c>Data:Provider</c> when a WorkerKit SQL service is resolved.</summary>
public enum WorkerKitSqlProvider
{
    /// <summary>Microsoft SQL Server, accessed through <c>Microsoft.Data.SqlClient</c>.</summary>
    SqlServer = 1,

    /// <summary>PostgreSQL, accessed through Npgsql.</summary>
    PostgreSql = 2,
}
