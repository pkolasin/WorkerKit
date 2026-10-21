namespace WorkerKit.Sql;

/// <summary>
/// Stages rows with <see cref="CopyToTemporaryAsync"/> and writes them to a permanent table.
/// An empty row list does not create a temporary table.
/// </summary>
public interface IWorkerKitBulkCopy
{
    /// <summary>
    /// Writes <paramref name="rows"/> into an existing temporary table.
    /// SQL Server uses <c>SqlBulkCopy</c>. PostgreSQL uses binary <c>COPY</c>.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="connection">Open <c>SqlConnection</c> or <c>NpgsqlConnection</c>.</param>
    /// <param name="transaction">Transaction the SQL Server bulk copy enlists in. PostgreSQL binary import uses the connection.</param>
    /// <param name="temporaryTable">Temporary table that already exists and has the mapped columns.</param>
    /// <param name="columns">Column maps. The list is not empty.</param>
    /// <param name="rows">Rows to write. An empty list completes the copy without adding rows.</param>
    /// <param name="cancellationToken">Token that cancels the copy.</param>
    /// <returns>A task that completes when the copy finishes.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">A name is empty or <paramref name="columns"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The connection is closed or is not a supported provider connection.</exception>
    Task CopyToTemporaryAsync<T>(
        DbConnection connection,
        DbTransaction? transaction,
        string temporaryTable,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a temporary table, copies <paramref name="rows"/>, inserts them into <paramref name="destinationTable"/>, and returns the new ids.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="connection">Open provider connection.</param>
    /// <param name="destinationTable">Permanent table whose id is generated.</param>
    /// <param name="idColumn">Generated id column. It is not one of <paramref name="columns"/>.</param>
    /// <param name="columns">Data columns.</param>
    /// <param name="rows">Rows to insert. An empty list returns an empty list and does not touch the database.</param>
    /// <param name="cancellationToken">Token that cancels the insert.</param>
    /// <returns>The generated ids, cast to <see cref="long"/>.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">A name is empty.</exception>
    /// <exception cref="InvalidOperationException">The connection is closed.</exception>
    Task<IReadOnlyList<long>> BulkInsertAsync<T>(
        DbConnection connection,
        string destinationTable,
        string idColumn,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages <paramref name="rows"/> and inserts keys that are not already present, ordered by <paramref name="keyColumns"/>, inside a short transaction.
    /// A unique violation or a deadlock retries that insert once. The destination needs a unique constraint on the key.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="connection">Open provider connection.</param>
    /// <param name="destinationTable">Permanent table.</param>
    /// <param name="keyColumns">Unique key. Staged keys in one call should be unique.</param>
    /// <param name="columns">Data columns. The key columns are included.</param>
    /// <param name="rows">Rows to upsert. An empty list does not touch the database.</param>
    /// <param name="cancellationToken">Token that cancels the upsert.</param>
    /// <returns>A task that completes when the missing rows are inserted or the conflict has been absorbed.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">A name is empty or a key column is not staged.</exception>
    /// <exception cref="InvalidOperationException">The connection is closed.</exception>
    Task BulkUpsertAsync<T>(
        DbConnection connection,
        string destinationTable,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Does what <see cref="BulkUpsertAsync{T}"/> does, then re-selects the ids of the staged keys.
    /// The re-select also runs when the insert hits a unique violation or a deadlock.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="connection">Open provider connection.</param>
    /// <param name="destinationTable">Permanent table.</param>
    /// <param name="idColumn">Id column. It is cast to <see cref="long"/>.</param>
    /// <param name="keyColumns">Unique key.</param>
    /// <param name="columns">Data columns.</param>
    /// <param name="rows">Rows to upsert.</param>
    /// <param name="cancellationToken">Token that cancels the upsert.</param>
    /// <returns>The ids of the staged keys, ordered by the key.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">A name is empty or a key column is not staged.</exception>
    /// <exception cref="InvalidOperationException">The connection is closed, or a staged key is still missing after the re-select.</exception>
    Task<IReadOnlyList<long>> BulkUpsertReturningIdsAsync<T>(
        DbConnection connection,
        string destinationTable,
        string idColumn,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<BulkColumn<T>> columns,
        IReadOnlyList<T> rows,
        CancellationToken cancellationToken = default);
}
