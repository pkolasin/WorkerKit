namespace WorkerKit.Sql;

/// <summary>
/// Applies <see cref="SchemaMigration"/> scripts in version order while holding the provider schema lock.
/// SQL Server takes <c>sp_getapplock</c> and runs each <c>GO</c> batch on that transaction.
/// PostgreSQL takes <c>pg_advisory_xact_lock</c> and runs the script as one batch.
/// A version less than or equal to the stored version is skipped. The runner does not downgrade.
/// </summary>
public sealed class SchemaMigrationRunner
{
    private readonly IWorkerKitSqlDialect _dialect;
    private readonly ISchemaVersionStore _store;
    private readonly IDbConnectionFactory _connections;

    /// <summary>Stores the dialect, the version store, and the connection factory.</summary>
    /// <param name="dialect">Dialect that locks and splits batches.</param>
    /// <param name="store">Version table used inside the lock transaction.</param>
    /// <param name="connections">Opens the connection. The provider was chosen when this factory was resolved.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public SchemaMigrationRunner(
        IWorkerKitSqlDialect dialect,
        ISchemaVersionStore store,
        IDbConnectionFactory connections)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(connections);
        _dialect = dialect;
        _store = store;
        _connections = connections;
    }

    /// <summary>
    /// Applies every migration whose version is greater than the stored version.
    /// The lock is held until the transaction commits. Each script runs inside that transaction.
    /// </summary>
    /// <param name="schemaName">Logical schema name stored in the version table.</param>
    /// <param name="migrations">Migrations to consider. Duplicate version numbers are rejected.</param>
    /// <param name="cancellationToken">Token that cancels a command. A cancelled migration rolls the transaction back.</param>
    /// <returns>A task that completes when the stored version is at least the highest applied version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schemaName"/> or <paramref name="migrations"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="schemaName"/> is empty or longer than 200 characters, or two migrations share a version.</exception>
    public async Task MigrateAsync(
        string schemaName,
        IReadOnlyList<SchemaMigration> migrations,
        CancellationToken cancellationToken = default)
    {
        var name = SqlText.NormalizeSchemaName(schemaName);
        ArgumentNullException.ThrowIfNull(migrations);
        var ordered = migrations.OrderBy(migration => migration.Version).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Version == ordered[index - 1].Version)
            {
                throw new ArgumentException(
                    $"Two migrations use version {ordered[index].Version}.",
                    nameof(migrations));
            }
        }

        await using var connection = await _connections.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await _dialect.AcquireSchemaLockAsync(connection, transaction, name, cancellationToken).ConfigureAwait(false);
        await _store.EnsureCreatedAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var current = await _store.GetVersionAsync(connection, transaction, name, cancellationToken).ConfigureAwait(false);
        foreach (var migration in ordered)
        {
            if (migration.Version <= current)
            {
                continue;
            }

            foreach (var batch in _dialect.SplitBatches(migration.Sql))
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = batch;
                command.CommandTimeout = 0;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await _store.SetVersionAsync(connection, transaction, name, migration.Version, cancellationToken)
                .ConfigureAwait(false);
            current = migration.Version;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
