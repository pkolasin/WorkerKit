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
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
