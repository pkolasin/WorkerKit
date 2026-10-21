using Dapper;

namespace WorkerKit.Sql;

/// <summary>
/// Reads and writes <c>worker_kit_schema_version</c> on a connection that already holds the schema lock.
/// </summary>
public interface ISchemaVersionStore
{
    /// <summary>Creates the version table when it is missing.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="transaction">Transaction that holds the schema lock.</param>
    /// <param name="cancellationToken">Token that cancels the command.</param>
    /// <returns>A task that completes when the table exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> or <paramref name="transaction"/> is null.</exception>
    Task EnsureCreatedAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Returns the applied version, or zero when <paramref name="schemaName"/> has no row.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="transaction">Transaction that holds the schema lock.</param>
    /// <param name="schemaName">Logical schema name.</param>
    /// <param name="cancellationToken">Token that cancels the command.</param>
    /// <returns>The stored version, or zero.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="schemaName"/> is empty or longer than 200 characters.</exception>
    Task<int> GetVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        string schemaName,
        CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="version"/> for <paramref name="schemaName"/>.</summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="transaction">Transaction that holds the schema lock.</param>
    /// <param name="schemaName">Logical schema name.</param>
    /// <param name="version">Version just applied. It is greater than zero.</param>
    /// <param name="cancellationToken">Token that cancels the command.</param>
    /// <returns>A task that completes when the version is stored.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="schemaName"/> is empty or longer than 200 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is less than 1.</exception>
    Task SetVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        string schemaName,
        int version,
        CancellationToken cancellationToken = default);
}
