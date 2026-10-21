using System.Text;

namespace WorkerKit.Sql;

public abstract partial class WorkerKitSqlDialect
{
    /// <summary>Physical name of the schema version table. The value is <c>worker_kit_schema_version</c>.</summary>
    public const string SchemaVersionTableName = "worker_kit_schema_version";

    /// <inheritdoc />
    public IReadOnlyList<string> SplitBatches(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var batches = new List<string>();
        var current = new StringBuilder();
        using var reader = new StringReader(script);
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsSqlServer)
                {
                    throw new InvalidOperationException(
                        "PostgreSQL scripts do not use GO batch separators. Remove the GO line or send one statement.");
                }

                AddBatch(batches, current);
                continue;
            }

            current.AppendLine(line);
        }

        AddBatch(batches, current);
        return batches;
    }

    /// <inheritdoc />
    public string BuildSchemaLockSql()
    {
        if (IsSqlServer)
        {
            return """
                DECLARE @result int;
                EXEC @result = sp_getapplock
                    @Resource = @Resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = -1;
                SELECT @result;
                """;
        }

        return "SELECT pg_advisory_xact_lock(@Key1, @Key2);";
    }

    /// <inheritdoc />
    public async Task AcquireSchemaLockAsync(
        DbConnection connection,
        DbTransaction transaction,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        var normalized = SqlText.NormalizeSchemaName(schemaName);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = BuildSchemaLockSql();
        if (IsSqlServer)
        {
            var resource = command.CreateParameter();
            resource.ParameterName = "@Resource";
            resource.Value = "WorkerKit.Schema:" + normalized;
            command.Parameters.Add(resource);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (result is int code && code < 0)
            {
                throw new InvalidOperationException($"SQL Server schema lock failed with code {code}.");
            }

            return;
        }

        var (key1, key2) = AdvisoryLockKey(normalized);
        var first = command.CreateParameter();
        first.ParameterName = "@Key1";
        first.Value = key1;
        var second = command.CreateParameter();
        second.ParameterName = "@Key2";
        second.Value = key2;
        command.Parameters.Add(first);
        command.Parameters.Add(second);
        await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public string BuildEnsureSchemaVersionTableSql()
    {
        if (IsSqlServer)
        {
            return """
                IF OBJECT_ID(N'dbo.worker_kit_schema_version', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.worker_kit_schema_version (
                        schema_name nvarchar(200) NOT NULL CONSTRAINT PK_worker_kit_schema_version PRIMARY KEY,
                        version int NOT NULL,
                        applied_at datetime2 NOT NULL
                    );
                END
                """;
        }

        return """
            CREATE TABLE IF NOT EXISTS worker_kit_schema_version (
                schema_name text NOT NULL PRIMARY KEY,
                version integer NOT NULL,
                applied_at timestamptz NOT NULL
            );
            """;
    }

    /// <inheritdoc />
    public string BuildGetSchemaVersionSql() => """
        SELECT version
        FROM worker_kit_schema_version
        WHERE schema_name = @SchemaName;
        """;

    /// <inheritdoc />
    public string BuildUpsertSchemaVersionSql()
    {
        if (IsSqlServer)
        {
            return """
                UPDATE dbo.worker_kit_schema_version
                SET version = @Version, applied_at = SYSUTCDATETIME()
                WHERE schema_name = @SchemaName;
                IF @@ROWCOUNT = 0
                BEGIN
                    INSERT INTO dbo.worker_kit_schema_version (schema_name, version, applied_at)
                    VALUES (@SchemaName, @Version, SYSUTCDATETIME());
                END
                """;
        }

        return """
            INSERT INTO worker_kit_schema_version (schema_name, version, applied_at)
            VALUES (@SchemaName, @Version, clock_timestamp())
            ON CONFLICT (schema_name) DO UPDATE
            SET version = EXCLUDED.version,
                applied_at = EXCLUDED.applied_at;
            """;
    }

    private static void AddBatch(List<string> batches, StringBuilder current)
    {
        var batch = current.ToString().Trim();
        if (batch.Length > 0)
        {
            batches.Add(batch);
        }

        current.Clear();
    }
}
