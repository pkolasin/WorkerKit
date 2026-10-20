using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WorkerKit.Hosting.Health;

/// <summary>JSON bodies, route matching, and status codes for <see cref="HealthEndpointWorker"/>.</summary>
public static class HealthResponses
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Informational version of the entry assembly, then that assembly's version, then <c>unknown</c>.
    /// When there is no entry assembly, uses the assembly that contains <see cref="HealthResponses"/>.
    /// </summary>
    /// <returns>The version string written as <c>version</c> in the health JSON.</returns>
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
    /// <param name="listenUrl">HttpListener prefix. It must be an absolute URI; otherwise the result is null.</param>
    /// <param name="absolutePath">Request path. Null is treated as <c>/</c>. Comparison ignores case and a trailing slash.</param>
    /// <returns><c>all</c>, <c>live</c>, <c>ready</c>, or null when the path is not one of those routes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="listenUrl"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="listenUrl"/> is empty or white space.</exception>
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
    /// <param name="route"><c>all</c>, <c>live</c>, <c>ready</c>, or another value. Only an ordinal match of <c>ready</c> treats Degraded as 503.</param>
    /// <param name="status">Aggregated status of the report for that route.</param>
    /// <returns>200 when the status is Healthy, or when it is Degraded and <paramref name="route"/> is not <c>ready</c>. Otherwise 503.</returns>
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

    /// <summary>
    /// Writes <paramref name="report"/> as JSON with <c>status</c>, <c>instance</c>, <c>version</c>, and <c>entries</c>.
    /// Each entry includes status, description, duration in the invariant constant format, and data.
    /// </summary>
    /// <param name="report">Report to serialize.</param>
    /// <param name="instanceName">Value of the <c>instance</c> property.</param>
    /// <param name="version">Value of the <c>version</c> property.</param>
    /// <returns>JSON produced with the web serializer defaults.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
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
