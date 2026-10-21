using Dapper;

namespace WorkerKit.Sql;

/// <summary>
/// Inserts a row when its key is missing and returns the surrogate id.
/// PostgreSQL uses <c>ON CONFLICT DO NOTHING</c> and re-reads when the statement returns no id.
/// SQL Server inserts and, on errors 2601 or 2627, re-reads the existing id.
/// Call the SQL Server path without a transaction: a duplicate key aborts the transaction that contained the insert.
/// </summary>
public static class WorkerKitInsert
{
    /// <summary>
    /// Runs <see cref="IWorkerKitSqlDialect.BuildInsertIfMissingSql"/> and returns the id.
    /// When the insert does not return an id, runs <see cref="IWorkerKitSqlDialect.BuildSelectIdByKeySql"/>.
    /// </summary>
    /// <param name="dialect">Dialect that renders the statements and classifies unique violations.</param>
    /// <param name="connection">Open connection.</param>
    /// <param name="transaction">Transaction for the PostgreSQL statement. Pass null for SQL Server.</param>
    /// <param name="insert">Table, columns, and key.</param>
    /// <param name="parameters">Parameter object whose names match <see cref="InsertIfMissingSql.Columns"/>.</param>
    /// <param name="cancellationToken">Token that cancels the commands.</param>
    /// <returns>The existing or new id, or null when the key is not present after the conflict.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">SQL Server reports a unique violation while <paramref name="transaction"/> is set.</exception>
    public static async Task<long?> InsertIfMissingAsync(
        IWorkerKitSqlDialect dialect,
        DbConnection connection,
        DbTransaction? transaction,
        InsertIfMissingSql insert,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(insert);
        ArgumentNullException.ThrowIfNull(parameters);
        try
        {
            var inserted = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
                dialect.BuildInsertIfMissingSql(insert),
                parameters,
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            if (inserted is not null)
            {
                return inserted;
            }
        }
        catch (Exception exception) when (dialect.RequiresDuplicateKeyRetry && dialect.IsUniqueViolation(exception))
        {
            if (transaction is not null)
            {
                throw new InvalidOperationException(
                    "SQL Server reported a duplicate key inside a transaction. Roll that transaction back and call InsertIfMissingAsync without a transaction so the existing id can be read.",
                    exception);
            }
        }

        return await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            dialect.BuildSelectIdByKeySql(insert),
            parameters,
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
