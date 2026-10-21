namespace WorkerKit.Sql;

/// <summary>Opens the connection selected by <c>Data:Provider</c> when this factory is resolved.</summary>
public sealed class WorkerKitConnectionFactory : IDbConnectionFactory
{
    private readonly IWorkerKitSqlDialect _dialect;
    private readonly IOptionsMonitor<DataOptions> _options;

    /// <summary>Stores the dialect and the data options.</summary>
    /// <param name="dialect">Dialect created from the provider that was current at resolution.</param>
    /// <param name="options">Options read again on each open, so the connection string can change after registration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dialect"/> or <paramref name="options"/> is null.</exception>
    public WorkerKitConnectionFactory(IWorkerKitSqlDialect dialect, IOptionsMonitor<DataOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(options);
        _dialect = dialect;
        _options = options;
    }

    /// <summary>Opens a connection for the current <see cref="DataOptions.ConnectionString"/>.</summary>
    /// <param name="cancellationToken">Token that cancels opening the connection.</param>
    /// <returns>The open connection. The caller disposes it.</returns>
    /// <exception cref="InvalidOperationException"><see cref="DataOptions.ConnectionString"/> is empty.</exception>
    public async ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Data:ConnectionString is required when Data:Provider is " + options.Provider + ".");
        }

        var connection = _dialect.CreateConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
