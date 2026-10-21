using System.Data;
using Dapper;
using Npgsql;
using NpgsqlTypes;

namespace WorkerKit.Sql;

/// <summary>
/// Copies rows into a temporary table and inserts or upserts them into a permanent table.
/// SQL Server uses <c>SqlBulkCopy</c>. PostgreSQL uses binary <c>COPY</c>.
/// Upsert inserts missing keys in key order inside a short transaction and re-selects after a unique violation or a deadlock.
/// </summary>
public sealed class WorkerKitBulkCopy : IWorkerKitBulkCopy
{
    private readonly IWorkerKitSqlDialect _dialect;

    /// <summary>Stores <paramref name="dialect"/>.</summary>
    /// <param name="dialect">Dialect that renders the staging SQL and classifies conflicts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialect"/> is null.</exception>
    public WorkerKitBulkCopy(IWorkerKitSqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        _dialect = dialect;
    }

    /// <inheritdoc />
    public async Task CopyToTemporaryAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        string temporaryTable,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default)
    {
        RequireOpen(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryTable);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        if (connection is Microsoft.Data.SqlClient.SqlConnection sql)
        {
            await CopySqlServerAsync(sql, transaction, temporaryTable, columns, rows, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (connection is NpgsqlConnection npgsql)
        {
            await CopyPostgreSqlAsync(npgsql, temporaryTable, columns, rows, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException(
            "CopyToTemporaryAsync requires an open SqlConnection or NpgsqlConnection.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> BulkInsertAsync<T>(
        DbConnection connection,
        string destinationTable,
        string idColumn,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default)
    {
        RequireOpen(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(idColumn);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return [];
        }

        var stage = _dialect.CreateTemporaryTableName();
        await CreateStageAsync(connection, stage, columns, cancellationToken).ConfigureAwait(false);
        try
        {
            await CopyToTemporaryAsync(connection, null, stage, columns, rows, cancellationToken).ConfigureAwait(false);
            var names = columns.Select(column => column.Name).ToArray();
            var sql = _dialect.BuildBulkInsertReturningIdsSql(destinationTable, stage, idColumn, names);
            var ids = await connection.QueryAsync<long>(
                new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return ids.ToArray();
        }
        finally
        {
            await DropQuietAsync(connection, stage, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task BulkUpsertAsync<T>(
        DbConnection connection,
        string destinationTable,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default)
    {
        await UpsertAsync(connection, destinationTable, idColumn: null, keyColumns, columns, rows, returningIds: false, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> BulkUpsertReturningIdsAsync<T>(
        DbConnection connection,
        string destinationTable,
        string idColumn,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idColumn);
        return await UpsertAsync(connection, destinationTable, idColumn, keyColumns, columns, rows, returningIds: true, cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    private async Task<IReadOnlyList<long>?> UpsertAsync<T>(
        DbConnection connection,
        string destinationTable,
        string? idColumn,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        bool returningIds,
        CancellationToken cancellationToken)
    {
        RequireOpen(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationTable);
        ArgumentNullException.ThrowIfNull(keyColumns);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return returningIds ? [] : null;
        }

        var stage = _dialect.CreateTemporaryTableName();
        var names = columns.Select(column => column.Name).ToArray();
        await CreateStageAsync(connection, stage, columns, cancellationToken).ConfigureAwait(false);
        try
        {
            await CopyToTemporaryAsync(connection, null, stage, columns, rows, cancellationToken).ConfigureAwait(false);
            var insert = _dialect.BuildInsertMissingSql(destinationTable, stage, names, keyColumns);
            var conflicted = await InsertMissingAsync(connection, insert, cancellationToken).ConfigureAwait(false);
            if (conflicted)
            {
                var staged = await CountStagedKeysAsync(connection, stage, keyColumns, cancellationToken).ConfigureAwait(false);
                var matched = await CountMatchedKeysAsync(connection, destinationTable, stage, keyColumns, cancellationToken).ConfigureAwait(false);
                if (matched < staged)
                {
                    throw new InvalidOperationException(
                        $"Bulk upsert stored {matched} of {staged} staged keys in '{destinationTable}' after a unique violation or deadlock.");
                }
            }

            if (!returningIds || idColumn is null)
            {
                return null;
            }

            var selected = _dialect.BuildReselectIdsSql(destinationTable, stage, idColumn, keyColumns);
            var ids = (await connection.QueryAsync<long>(
                new CommandDefinition(selected, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
            var expected = await CountStagedKeysAsync(connection, stage, keyColumns, cancellationToken).ConfigureAwait(false);
            if (ids.Length < expected)
            {
                throw new InvalidOperationException(
                    $"Bulk upsert re-selected {ids.Length} of {expected} staged keys in '{destinationTable}'.");
            }

            return ids;
        }
        finally
        {
            await DropQuietAsync(connection, stage, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> InsertMissingAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteInShortTransactionAsync(connection, sql, cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (Exception exception) when (IsConflict(exception))
        {
        }

        try
        {
            await ExecuteInShortTransactionAsync(connection, sql, cancellationToken).ConfigureAwait(false);
            return false;
        }
        catch (Exception exception) when (IsConflict(exception))
        {
            return true;
        }
    }

    private async Task ExecuteInShortTransactionAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 0;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool IsConflict(Exception exception) =>
        _dialect.IsUniqueViolation(exception) || _dialect.IsDeadlock(exception);

    private async Task<int> CountStagedKeysAsync(
        DbConnection connection,
        string stage,
        IReadOnlyList<string> keyColumns,
        CancellationToken cancellationToken)
    {
        var sql = _dialect.BuildCountStagedKeysSql(stage, keyColumns);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<int> CountMatchedKeysAsync(
        DbConnection connection,
        string destination,
        string stage,
        IReadOnlyList<string> keyColumns,
        CancellationToken cancellationToken)
    {
        var sql = _dialect.BuildCountMatchedKeysSql(destination, stage, keyColumns);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task CreateStageAsync<T>(
        DbConnection connection,
        string stage,
        IReadOnlyList<BulkColumn<T>> columns,
        CancellationToken cancellationToken)
    {
        var definitions = columns.Select(column => column.Definition).ToArray();
        var sql = _dialect.BuildCreateTemporaryTableSql(stage, definitions);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DropQuietAsync(DbConnection connection, string stage, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = _dialect.BuildDropTemporaryTableSql(stage);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }

    private static void RequireOpen(DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("The connection must be open.");
        }
    }

    private static async Task CopySqlServerAsync<T>(
        Microsoft.Data.SqlClient.SqlConnection connection,
        DbTransaction? transaction,
        string temporaryTable,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken)
    {
        using var table = new System.Data.DataTable();
        foreach (var column in columns)
        {
            var dataColumn = table.Columns.Add(column.Name, column.ClrType == typeof(DateOnly) ? typeof(DateTime) : column.ClrType);
            dataColumn.AllowDBNull = true;
        }

        foreach (var row in rows)
        {
            var values = new object[columns.Count];
            for (var index = 0; index < columns.Count; index++)
            {
                values[index] = Box(columns[index].Value(row));
            }

            table.Rows.Add(values);
        }

        using var bulk = new Microsoft.Data.SqlClient.SqlBulkCopy(
            connection,
            Microsoft.Data.SqlClient.SqlBulkCopyOptions.TableLock,
            transaction as Microsoft.Data.SqlClient.SqlTransaction)
        {
            DestinationTableName = temporaryTable,
            BulkCopyTimeout = 0,
        };
        foreach (System.Data.DataColumn column in table.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulk.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);
    }

    private async Task CopyPostgreSqlAsync<T>(
        NpgsqlConnection connection,
        string temporaryTable,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken)
    {
        var table = _dialect.QuoteIdentifier(temporaryTable);
        var names = string.Join(", ", columns.Select(column => _dialect.QuoteIdentifier(column.Name)));
        await using var writer = await connection.BeginBinaryImportAsync(
            $"COPY {table} ({names}) FROM STDIN (FORMAT BINARY)",
            cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
        {
            await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);
            foreach (var column in columns)
            {
                await WritePostgreSqlValueAsync(writer, column.Value(row), cancellationToken).ConfigureAwait(false);
            }
        }

        await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WritePostgreSqlValueAsync(
        NpgsqlBinaryImporter writer,
        object? value,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            await writer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        switch (value)
        {
            case string text:
                await writer.WriteAsync(text, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
                break;
            case int number:
                await writer.WriteAsync(number, NpgsqlDbType.Integer, cancellationToken).ConfigureAwait(false);
                break;
            case long number:
                await writer.WriteAsync(number, NpgsqlDbType.Bigint, cancellationToken).ConfigureAwait(false);
                break;
            case short number:
                await writer.WriteAsync(number, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                break;
            case byte number:
                await writer.WriteAsync((short)number, NpgsqlDbType.Smallint, cancellationToken).ConfigureAwait(false);
                break;
            case bool flag:
                await writer.WriteAsync(flag, NpgsqlDbType.Boolean, cancellationToken).ConfigureAwait(false);
                break;
            case DateTime instant:
                await writer.WriteAsync(instant, NpgsqlDbType.Timestamp, cancellationToken).ConfigureAwait(false);
                break;
            case DateTimeOffset instant:
                await writer.WriteAsync(instant, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                break;
            case DateOnly date:
                await writer.WriteAsync(date, NpgsqlDbType.Date, cancellationToken).ConfigureAwait(false);
                break;
            case Guid id:
                await writer.WriteAsync(id, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                break;
            case decimal number:
                await writer.WriteAsync(number, NpgsqlDbType.Numeric, cancellationToken).ConfigureAwait(false);
                break;
            case double number:
                await writer.WriteAsync(number, NpgsqlDbType.Double, cancellationToken).ConfigureAwait(false);
                break;
            case float number:
                await writer.WriteAsync(number, NpgsqlDbType.Real, cancellationToken).ConfigureAwait(false);
                break;
            case byte[] bytes:
                await writer.WriteAsync(bytes, NpgsqlDbType.Bytea, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Cannot copy a value of type '{value.GetType().FullName}'.");
        }
    }

    private static object Box(object? value)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        if (value is DateOnly date)
        {
            return date.ToDateTime(TimeOnly.MinValue);
        }

        return value;
    }
}
