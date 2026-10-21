namespace WorkerKit.Sql;

/// <summary>Creates a dialect from <c>Data:Provider</c> without reading configuration at registration.</summary>
public static class WorkerKitSql
{
    /// <summary>
    /// Parses <paramref name="provider"/>.
    /// Accepted values are <c>SqlServer</c>, <c>PostgreSQL</c>, and <c>Postgres</c>. Comparison is case-insensitive.
    /// </summary>
    /// <param name="provider">Value of <c>Data:Provider</c>.</param>
    /// <param name="parsed">The provider when this method returns true.</param>
    /// <returns>True when <paramref name="provider"/> names SQL Server or PostgreSQL.</returns>
    public static bool TryParseProvider(string? provider, out WorkerKitSqlProvider parsed)
    {
        var value = provider?.Trim() ?? "";
        if (value.Equals(DataOptions.SqlServerProvider, StringComparison.OrdinalIgnoreCase))
        {
            parsed = WorkerKitSqlProvider.SqlServer;
            return true;
        }

        if (value.Equals(DataOptions.PostgreSqlProvider, StringComparison.OrdinalIgnoreCase)
            || value.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            parsed = WorkerKitSqlProvider.PostgreSql;
            return true;
        }

        parsed = default;
        return false;
    }

    /// <summary>Creates the dialect for <paramref name="provider"/>.</summary>
    /// <param name="provider">Value of <c>Data:Provider</c>.</param>
    /// <returns>A <see cref="SqlServerDialect"/> or a <see cref="PostgreSqlDialect"/>.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="provider"/> is not SQL Server or PostgreSQL.</exception>
    public static IWorkerKitSqlDialect CreateDialect(string? provider)
    {
        if (!TryParseProvider(provider, out var parsed))
        {
            throw new InvalidOperationException(
                "Data:Provider must be SqlServer or PostgreSQL when the WorkerKit SQL dialect is resolved. The value is read when the dialect is first resolved, not when AddWorkerKitSql is called.");
        }

        return parsed == WorkerKitSqlProvider.SqlServer
            ? new SqlServerDialect()
            : new PostgreSqlDialect();
    }
}
