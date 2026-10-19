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

    public InstanceDirectoriesHealthCheck(IInstanceContext instance)
    {
        _instance = instance;
    }

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
