using System.Data.Common;

namespace WorkerKit.Abstractions.Data;

/// <summary>
/// Opens a database connection for the configured <c>Data</c> provider.
/// The readiness check runs <c>SELECT 1</c> on the connection. The host does not register
/// an implementation; a connector registers one when <c>Data:Provider</c> is not <c>None</c>.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Opens a connection. The caller disposes it.</summary>
    /// <param name="cancellationToken">Token the caller can use to cancel opening the connection.</param>
    /// <returns>The open connection. The caller disposes it.</returns>
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
