using Dapper;

namespace WorkerKit.Sql;

/// <summary><see cref="ISchemaVersionStore"/> that uses <see cref="IWorkerKitSqlDialect"/> for the version table.</summary>
public sealed class SchemaVersionStore : ISchemaVersionStore
{
    private readonly IWorkerKitSqlDialect _dialect;

    /// <summary>Stores <paramref name="dialect"/>.</summary>
    /// <param name="dialect">Dialect that renders the version-table statements.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialect"/> is null.</exception>
    public SchemaVersionStore(IWorkerKitSqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        _dialect = dialect;
    }

    /// <inheritdoc />
    public async Task EnsureCreatedAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Require(connection, transaction);
        await connection.ExecuteAsync(new CommandDefinition(
            _dialect.BuildEnsureSchemaVersionTableSql(),
            transaction: transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        string schemaName,
        CancellationToken cancellationToken = default)
    {
        Require(connection, transaction);
        var name = SqlText.NormalizeSchemaName(schemaName);
        var version = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            _dialect.BuildGetSchemaVersionSql(),
            new { SchemaName = name },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return version ?? 0;
    }

    /// <inheritdoc />
    public async Task SetVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        string schemaName,
        int version,
        CancellationToken cancellationToken = default)
    {
        Require(connection, transaction);
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Schema version must be at least 1.");
        }

        var name = SqlText.NormalizeSchemaName(schemaName);
        await connection.ExecuteAsync(new CommandDefinition(
            _dialect.BuildUpsertSchemaVersionSql(),
            new { SchemaName = name, Version = version },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static void Require(DbConnection connection, DbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
    }
}
