using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WorkerKit.Hosting.Health;

public static class HealthResponses
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string HostVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(HealthResponses).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }

    /// <summary>
    /// <paramref name="listenUrl"/> is the HttpListener prefix. Returns <c>all</c>, <c>live</c>, <c>ready</c>, or null.
    /// </summary>
    public static string? Match(string listenUrl, string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listenUrl);
        if (!Uri.TryCreate(listenUrl.Trim(), UriKind.Absolute, out var listen))
        {
            return null;
        }

        var prefix = listen.AbsolutePath.TrimEnd('/');
        var path = (absolutePath ?? "/").TrimEnd('/');
        if (string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase))
        {
            return "all";
        }

        if (string.Equals(path, prefix + "/live", StringComparison.OrdinalIgnoreCase))
        {
            return "live";
        }

        if (string.Equals(path, prefix + "/ready", StringComparison.OrdinalIgnoreCase))
        {
            return "ready";
        }

        return null;
    }

    /// <summary>
    /// Liveness and the combined report return 503 only for Unhealthy, so a Degraded iteration does not
    /// look like a dead process. Readiness returns 503 for Degraded and Unhealthy.
    /// </summary>
    public static int StatusCode(string route, HealthStatus status)
    {
        if (status == HealthStatus.Healthy)
        {
            return 200;
        }

        if (status == HealthStatus.Degraded && !string.Equals(route, "ready", StringComparison.Ordinal))
        {
            return 200;
        }

        return 503;
    }

    public static string Serialize(HealthReport report, string instanceName, string version)
    {
        ArgumentNullException.ThrowIfNull(report);
        var entries = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, entry) in report.Entries)
        {
            entries[name] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = entry.Status.ToString(),
                ["description"] = entry.Description,
                ["duration"] = entry.Duration.ToString("c", CultureInfo.InvariantCulture),
                ["data"] = entry.Data.Count == 0 ? new Dictionary<string, object>() : entry.Data,
            };
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = report.Status.ToString(),
            ["instance"] = instanceName,
            ["version"] = version,
            ["entries"] = entries,
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
