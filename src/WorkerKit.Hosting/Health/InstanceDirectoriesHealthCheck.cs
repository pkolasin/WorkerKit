using Microsoft.Extensions.Diagnostics.HealthChecks;

using WorkerKit.Abstractions.Instances;
namespace WorkerKit.Hosting.Health;

/// <summary>
/// Readiness: the instance data and temp directories must accept a write.
/// A directory that is missing or not writable is Unhealthy.
/// </summary>
public sealed class InstanceDirectoriesHealthCheck : IHealthCheck
{
    private readonly IInstanceContext _instance;

    /// <summary>Stores the instance whose data and temp directories are probed.</summary>
    /// <param name="instance">Supplies <see cref="IInstanceContext.DataDirectory"/> and <see cref="IInstanceContext.TempDirectory"/>.</param>
    public InstanceDirectoriesHealthCheck(IInstanceContext instance)
    {
        _instance = instance;
    }

    /// <summary>
    /// Writes a temporary file in the data directory and the temp directory, then deletes it.
    /// A missing directory or a failed write is Unhealthy. Both paths are included in the result data.
    /// </summary>
    /// <param name="context">Health check context. This check does not read it.</param>
    /// <param name="cancellationToken">Unused. The probe is synchronous.</param>
    /// <returns>Healthy when both directories accept a write; otherwise Unhealthy.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();
        Probe(_instance.DataDirectory, "data", failures);
        Probe(_instance.TempDirectory, "temp", failures);
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["dataDirectory"] = _instance.DataDirectory,
            ["tempDirectory"] = _instance.TempDirectory,
        };
        if (failures.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(string.Join("; ", failures), data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("instance data and temp directories are writable", data));
    }

    private static void Probe(string directory, string name, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            failures.Add($"{name} directory '{directory}' does not exist");
            return;
        }

        var probe = Path.Combine(directory, ".health-write-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception exception)
        {
            failures.Add($"{name} directory '{directory}' is not writable: {exception.Message}");
            try
            {
                File.Delete(probe);
            }
            catch (Exception delete)
            {
                _ = delete;
            }
        }
    }
}
