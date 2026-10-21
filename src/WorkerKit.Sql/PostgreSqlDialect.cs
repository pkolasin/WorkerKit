namespace WorkerKit.Sql;

/// <summary>PostgreSQL dialect. Claims use <c>FOR UPDATE SKIP LOCKED</c>. Insert-if-missing uses <c>ON CONFLICT DO NOTHING</c>.</summary>
public sealed class PostgreSqlDialect : WorkerKitSqlDialect
{
    /// <summary>Creates the PostgreSQL dialect.</summary>
    public PostgreSqlDialect()
        : base(WorkerKitSqlProvider.PostgreSql)
    {
    }
}
