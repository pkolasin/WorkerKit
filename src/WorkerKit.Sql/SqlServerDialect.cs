namespace WorkerKit.Sql;

/// <summary>SQL Server dialect. Claims use <c>READPAST</c> and <c>UPDLOCK</c>. Insert-if-missing retries duplicate keys.</summary>
public sealed class SqlServerDialect : WorkerKitSqlDialect
{
    /// <summary>Creates the SQL Server dialect.</summary>
    public SqlServerDialect()
        : base(WorkerKitSqlProvider.SqlServer)
    {
    }
}
