namespace WorkerKit.Sql;

public abstract partial class WorkerKitSqlDialect
{
    /// <inheritdoc />
    public string BuildClaimSql(WorkLeaseSql lease)
    {
        var names = LeaseNames.Read(this, lease);
        return IsSqlServer ? names.SqlServerClaim() : names.PostgreSqlClaim();
    }

    /// <inheritdoc />
    public string BuildClaimByIdSql(WorkLeaseSql lease)
    {
        var names = LeaseNames.Read(this, lease);
        return IsSqlServer ? names.SqlServerClaimById() : names.PostgreSqlClaimById();
    }

    /// <inheritdoc />
    public string BuildRenewLeaseSql(WorkLeaseSql lease)
    {
        var names = LeaseNames.Read(this, lease);
        return IsSqlServer ? names.SqlServerRenew() : names.PostgreSqlRenew();
    }

    /// <inheritdoc />
    public string BuildInsertIfMissingSql(InsertIfMissingSql insert)
    {
        ArgumentNullException.ThrowIfNull(insert);
        EnsureKeysAreColumns(insert);
        var table = QuoteIdentifier(insert.Table);
        var columns = insert.Columns.Select(QuoteIdentifier).ToArray();
        var parameters = insert.Columns.Select(SqlText.Parameter).ToArray();
        var columnList = string.Join(", ", columns);
        var valueList = string.Join(", ", parameters);
        if (!IsSqlServer)
        {
            var keys = string.Join(", ", insert.KeyColumns.Select(QuoteIdentifier));
            var returning = CastId(QuoteIdentifier(insert.IdColumn), QuoteAlias(insert.IdAlias));
            return $"""
                INSERT INTO {table} ({columnList})
                VALUES ({valueList})
                ON CONFLICT ({keys}) DO NOTHING
                RETURNING {returning};
                """;
        }

        var output = CastId($"inserted.{QuoteIdentifier(insert.IdColumn)}", QuoteAlias(insert.IdAlias));
        return $"""
            INSERT INTO {table} ({columnList})
            OUTPUT {output}
            VALUES ({valueList});
            """;
    }

    /// <inheritdoc />
    public string BuildSelectIdByKeySql(InsertIfMissingSql insert)
    {
        ArgumentNullException.ThrowIfNull(insert);
        var table = QuoteIdentifier(insert.Table);
        var id = CastId(QuoteIdentifier(insert.IdColumn), QuoteAlias(insert.IdAlias));
        var filter = string.Join(" AND ", insert.KeyColumns.Select(column =>
            $"{QuoteIdentifier(column)} = {SqlText.Parameter(column)}"));
        return $"""
            SELECT {id}
            FROM {table}
            WHERE {filter};
            """;
    }

    private string CastId(string quotedColumn, string quotedAlias)
    {
        return IsSqlServer
            ? $"CAST({quotedColumn} AS bigint) AS {quotedAlias}"
            : $"{quotedColumn}::bigint AS {quotedAlias}";
    }

    private static void EnsureKeysAreColumns(InsertIfMissingSql insert)
    {
        foreach (var key in insert.KeyColumns)
        {
            if (!insert.Columns.Contains(key, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Key column '{key}' must also be one of the inserted columns.",
                    nameof(insert));
            }
        }
    }

    private sealed class LeaseNames
    {
        private LeaseNames(
            string table,
            string id,
            string available,
            string until,
            string attempt,
            string workerColumn,
            string returning,
            string max,
            string seconds,
            string worker,
            string idParameter)
        {
            Table = table;
            Id = id;
            Available = available;
            Until = until;
            Attempt = attempt;
            WorkerColumn = workerColumn;
            Returning = returning;
            Max = max;
            Seconds = seconds;
            Worker = worker;
            IdParameter = idParameter;
        }

        private string Table { get; }

        private string Id { get; }

        private string Available { get; }

        private string Until { get; }

        private string Attempt { get; }

        private string WorkerColumn { get; }

        private string Returning { get; }

        private string Max { get; }

        private string Seconds { get; }

        private string Worker { get; }

        private string IdParameter { get; }

        public static LeaseNames Read(WorkerKitSqlDialect dialect, WorkLeaseSql lease)
        {
            ArgumentNullException.ThrowIfNull(lease);
            if (lease.ReturningColumns is null || lease.ReturningColumns.Count == 0)
            {
                throw new ArgumentException("At least one returning column is required.", nameof(lease));
            }

            return new LeaseNames(
                dialect.QuoteIdentifier(lease.Table),
                dialect.QuoteIdentifier(lease.IdColumn),
                dialect.QuoteIdentifier(lease.AvailableAtColumn),
                dialect.QuoteIdentifier(lease.LeaseUntilColumn),
                dialect.QuoteIdentifier(lease.AttemptColumn),
                dialect.QuoteIdentifier(lease.LeasedByColumn),
                dialect.ReturningList(lease.ReturningColumns),
                SqlText.Parameter(lease.MaxParameter),
                SqlText.Parameter(lease.LeaseSecondsParameter),
                SqlText.Parameter(lease.WorkerParameter),
                SqlText.Parameter(lease.IdParameter));
        }

        public string SqlServerClaim() => $"""
            ;WITH claim AS (
                SELECT TOP ({Max}) target.{Id}
                FROM {Table} AS target WITH (READPAST, UPDLOCK, ROWLOCK)
                WHERE target.{Available} <= SYSUTCDATETIME()
                  AND (target.{Until} IS NULL OR target.{Until} < SYSUTCDATETIME())
                ORDER BY target.{Available}, target.{Id}
            )
            UPDATE target
            SET {Until} = DATEADD(second, {Seconds}, SYSUTCDATETIME()),
                {Attempt} = target.{Attempt} + 1,
                {WorkerColumn} = {Worker}
            OUTPUT {Returning}
            FROM {Table} AS target
            INNER JOIN claim ON target.{Id} = claim.{Id};
            """;

        public string PostgreSqlClaim() => $"""
            UPDATE {Table} AS target
            SET {Until} = clock_timestamp() + ({Seconds} * interval '1 second'),
                {Attempt} = target.{Attempt} + 1,
                {WorkerColumn} = {Worker}
            WHERE target.{Id} IN (
                SELECT candidate.{Id}
                FROM {Table} AS candidate
                WHERE candidate.{Available} <= clock_timestamp()
                  AND (candidate.{Until} IS NULL OR candidate.{Until} < clock_timestamp())
                ORDER BY candidate.{Available}, candidate.{Id}
                LIMIT {Max}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING {Returning};
            """;

        public string SqlServerClaimById() => $"""
            UPDATE target
            SET {Until} = DATEADD(second, {Seconds}, SYSUTCDATETIME()),
                {Attempt} = target.{Attempt} + 1,
                {WorkerColumn} = {Worker}
            OUTPUT {Returning}
            FROM {Table} AS target WITH (READPAST, UPDLOCK, ROWLOCK)
            WHERE target.{Id} = {IdParameter}
              AND (target.{Until} IS NULL OR target.{Until} < SYSUTCDATETIME());
            """;

        public string PostgreSqlClaimById() => $"""
            UPDATE {Table} AS target
            SET {Until} = clock_timestamp() + ({Seconds} * interval '1 second'),
                {Attempt} = target.{Attempt} + 1,
                {WorkerColumn} = {Worker}
            WHERE target.{Id} = (
                SELECT candidate.{Id}
                FROM {Table} AS candidate
                WHERE candidate.{Id} = {IdParameter}
                  AND (candidate.{Until} IS NULL OR candidate.{Until} < clock_timestamp())
                FOR UPDATE SKIP LOCKED
            )
            RETURNING {Returning};
            """;

        public string SqlServerRenew() => $"""
            UPDATE target
            SET {Until} = DATEADD(second, {Seconds}, SYSUTCDATETIME())
            OUTPUT {Returning}
            FROM {Table} AS target WITH (UPDLOCK, ROWLOCK)
            WHERE target.{Id} = {IdParameter}
              AND target.{WorkerColumn} = {Worker}
              AND target.{Until} >= SYSUTCDATETIME();
            """;

        public string PostgreSqlRenew() => $"""
            UPDATE {Table} AS target
            SET {Until} = clock_timestamp() + ({Seconds} * interval '1 second')
            WHERE target.{Id} = {IdParameter}
              AND target.{WorkerColumn} = {Worker}
              AND target.{Until} >= clock_timestamp()
            RETURNING {Returning};
            """;
    }

    private string ReturningList(IReadOnlyList<string> columns)
    {
        var source = IsSqlServer ? "inserted" : "target";
        return string.Join(", ", columns.Select(column =>
        {
            var quoted = QuoteIdentifier(column);
            return $"{source}.{quoted} AS {QuoteAlias(column)}";
        }));
    }
}
