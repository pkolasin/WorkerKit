namespace WorkerKit.Sql;

public abstract partial class WorkerKitSqlDialect
{
    /// <inheritdoc />
    public string BuildCreateTemporaryTableSql(string tableName, IReadOnlyList<BulkColumnDefinition> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        var table = QuoteTemporary(tableName);
        var definitions = string.Join(", ", columns.Select(column =>
            $"{QuoteIdentifier(column.Name)} {SqlTypeNames.Format(column.ClrType, IsSqlServer)} NULL"));
        var temporary = IsSqlServer ? "" : "TEMP ";
        return $"CREATE {temporary}TABLE {table} ({definitions});";
    }

    /// <inheritdoc />
    public string BuildDropTemporaryTableSql(string tableName) =>
        $"DROP TABLE IF EXISTS {QuoteTemporary(tableName)};";

    /// <inheritdoc />
    public string BuildBulkInsertReturningIdsSql(
        string destinationTable,
        string stageTable,
        string idColumn,
        IReadOnlyList<string> columns)
    {
        var destination = QuoteIdentifier(destinationTable);
        var stage = QuoteTemporary(stageTable);
        var columnList = ColumnList(columns);
        var selectList = string.Join(", ", columns.Select(QuoteIdentifier));
        if (IsSqlServer)
        {
            var id = CastId($"inserted.{QuoteIdentifier(idColumn)}", QuoteAlias(idColumn));
            return $"""
                INSERT INTO {destination} ({columnList})
                OUTPUT {id}
                SELECT {selectList}
                FROM {stage};
                """;
        }

        var returning = CastId(QuoteIdentifier(idColumn), QuoteAlias(idColumn));
        return $"""
            INSERT INTO {destination} ({columnList})
            SELECT {selectList}
            FROM {stage}
            RETURNING {returning};
            """;
    }

    /// <inheritdoc />
    public string BuildInsertMissingSql(
        string destinationTable,
        string stageTable,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> keyColumns)
    {
        EnsureKeys(columns, keyColumns);
        var destination = QuoteIdentifier(destinationTable);
        var stage = QuoteTemporary(stageTable);
        var columnList = ColumnList(columns);
        var keyList = string.Join(", ", keyColumns.Select(QuoteIdentifier));
        var projected = string.Join(", ", columns.Select(QuoteIdentifier));
        var selected = string.Join(", ", columns.Select(column => $"stage.{QuoteIdentifier(column)}"));
        var match = string.Join(" AND ", keyColumns.Select(column =>
        {
            var quoted = QuoteIdentifier(column);
            return $"existing.{quoted} = stage.{quoted}";
        }));
        var lockHint = IsSqlServer ? " WITH (UPDLOCK, ROWLOCK)" : "";
        return $"""
            INSERT INTO {destination} ({columnList})
            SELECT {selected}
            FROM (
                SELECT {projected},
                       ROW_NUMBER() OVER (PARTITION BY {keyList} ORDER BY {keyList}) AS wk_rn
                FROM {stage}
            ) AS stage
            WHERE stage.wk_rn = 1
              AND NOT EXISTS (
                  SELECT 1
                  FROM {destination} AS existing{lockHint}
                  WHERE {match}
              )
            ORDER BY {string.Join(", ", keyColumns.Select(column => $"stage.{QuoteIdentifier(column)}"))};
            """;
    }

    /// <inheritdoc />
    public string BuildReselectIdsSql(
        string destinationTable,
        string stageTable,
        string idColumn,
        IReadOnlyList<string> keyColumns)
    {
        ArgumentNullException.ThrowIfNull(keyColumns);
        if (keyColumns.Count == 0)
        {
            throw new ArgumentException("At least one key column is required.", nameof(keyColumns));
        }

        var destination = QuoteIdentifier(destinationTable);
        var stage = QuoteTemporary(stageTable);
        var id = CastId($"existing.{QuoteIdentifier(idColumn)}", QuoteAlias(idColumn));
        var keys = string.Join(", ", keyColumns.Select(QuoteIdentifier));
        var match = string.Join(" AND ", keyColumns.Select(column =>
        {
            var quoted = QuoteIdentifier(column);
            return $"existing.{quoted} = stage.{quoted}";
        }));
        var order = string.Join(", ", keyColumns.Select(column => $"existing.{QuoteIdentifier(column)}"));
        return $"""
            SELECT {id}
            FROM {destination} AS existing
            INNER JOIN (
                SELECT DISTINCT {keys}
                FROM {stage}
            ) AS stage ON {match}
            ORDER BY {order};
            """;
    }

    /// <inheritdoc />
    public string BuildCountStagedKeysSql(string stageTable, IReadOnlyList<string> keyColumns)
    {
        ArgumentNullException.ThrowIfNull(keyColumns);
        if (keyColumns.Count == 0)
        {
            throw new ArgumentException("At least one key column is required.", nameof(keyColumns));
        }

        var stage = QuoteTemporary(stageTable);
        var keys = string.Join(", ", keyColumns.Select(QuoteIdentifier));
        var count = CountExpression();
        return $"""
            SELECT {count}
            FROM (
                SELECT DISTINCT {keys}
                FROM {stage}
            ) AS stage;
            """;
    }

    /// <inheritdoc />
    public string BuildCountMatchedKeysSql(string destinationTable, string stageTable, IReadOnlyList<string> keyColumns)
    {
        ArgumentNullException.ThrowIfNull(keyColumns);
        if (keyColumns.Count == 0)
        {
            throw new ArgumentException("At least one key column is required.", nameof(keyColumns));
        }

        var destination = QuoteIdentifier(destinationTable);
        var stage = QuoteTemporary(stageTable);
        var keys = string.Join(", ", keyColumns.Select(QuoteIdentifier));
        var match = string.Join(" AND ", keyColumns.Select(column =>
        {
            var quoted = QuoteIdentifier(column);
            return $"existing.{quoted} = stage.{quoted}";
        }));
        var count = CountExpression();
        return $"""
            SELECT {count}
            FROM (
                SELECT DISTINCT {keys}
                FROM {stage}
            ) AS stage
            INNER JOIN {destination} AS existing ON {match};
            """;
    }

    private string ColumnList(IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        return string.Join(", ", columns.Select(QuoteIdentifier));
    }

    private static void EnsureKeys(IReadOnlyList<string> columns, IReadOnlyList<string> keyColumns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(keyColumns);
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        if (keyColumns.Count == 0)
        {
            throw new ArgumentException("At least one key column is required.", nameof(keyColumns));
        }

        foreach (var key in keyColumns)
        {
            if (!columns.Contains(key, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Key column '{key}' must also be one of the staged columns.", nameof(keyColumns));
            }
        }
    }
}
