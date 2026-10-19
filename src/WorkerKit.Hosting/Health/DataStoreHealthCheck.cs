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

    public DataStoreHealthCheck(IOptionsMonitor<DataOptions> data, IEnumerable<IDbConnectionFactory> factories)
    {
        _data = data;
        _factory = factories.SingleOrDefault();
    }

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
