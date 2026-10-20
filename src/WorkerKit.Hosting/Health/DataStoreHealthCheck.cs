using Microsoft.Extensions.Diagnostics.HealthChecks;

using WorkerKit.Abstractions.Data;
using WorkerKit.Abstractions.Options;
namespace WorkerKit.Hosting.Health;

/// <summary>
/// Readiness probe for the optional database. <c>Data:Provider</c> <c>None</c> is Healthy and does not open a connection.
/// Any other provider runs <c>SELECT 1</c> through <see cref="IDbConnectionFactory"/>.
/// </summary>
public sealed class DataStoreHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<DataOptions> _data;
    private readonly IDbConnectionFactory? _factory;

    /// <summary>
    /// Keeps the data options and the single registered <see cref="IDbConnectionFactory"/>.
    /// </summary>
    /// <param name="data">Current <c>Data</c> options. <see cref="DataOptions.Provider"/> selects the probe.</param>
    /// <param name="factories">Registered connection factories. The check uses the only entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factories"/> is null.</exception>
    /// <exception cref="InvalidOperationException">More than one <see cref="IDbConnectionFactory"/> is registered.</exception>
    public DataStoreHealthCheck(IOptionsMonitor<DataOptions> data, IEnumerable<IDbConnectionFactory> factories)
    {
        _data = data;
        _factory = factories.SingleOrDefault();
    }

    /// <summary>
    /// Returns Healthy when <see cref="DataOptions.Provider"/> is missing or <c>None</c>, without opening a connection.
    /// Any other provider runs <c>SELECT 1</c>. A configured provider with no factory is Unhealthy.
    /// A probe that throws is Unhealthy and keeps that exception.
    /// </summary>
    /// <param name="context">Health check context. This check does not read it.</param>
    /// <param name="cancellationToken">Passed to <see cref="IDbConnectionFactory.OpenConnectionAsync"/> and the probe command.</param>
    /// <returns>Healthy when the store is not configured or answers <c>SELECT 1</c>; otherwise Unhealthy.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var provider = _data.CurrentValue.Provider?.Trim() ?? "";
        if (provider.Length == 0 || provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("data store is not configured");
        }

        if (_factory is null)
        {
            return HealthCheckResult.Unhealthy(
                "Data:Provider is set, but IDbConnectionFactory is not registered. Register it from ConfigureHost.");
        }

        try
        {
            await using var connection = await _factory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("data store answered SELECT 1");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("data store probe failed: " + exception.Message, exception);
        }
    }
}
