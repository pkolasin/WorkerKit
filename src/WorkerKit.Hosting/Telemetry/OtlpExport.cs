namespace WorkerKit.Hosting.Telemetry;

public enum OtlpProtocolKind
{
    Grpc,
    HttpProtobuf,
}

/// <summary>
/// OTLP settings read from the standard <c>OTEL_EXPORTER_OTLP_*</c> configuration keys.
/// Export stays off until <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set. The .NET exporter and the
/// Serilog sink both default to gRPC on port 4317 when the protocol is omitted.
/// </summary>
public sealed record OtlpLogExportSettings(
    string Endpoint,
    string? LogsEndpoint,
    OtlpProtocolKind Protocol,
    IReadOnlyDictionary<string, string> Headers)
{
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string LogsEndpointKey = "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT";
    public const string ProtocolKey = "OTEL_EXPORTER_OTLP_PROTOCOL";
    public const string LogsProtocolKey = "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL";
    public const string HeadersKey = "OTEL_EXPORTER_OTLP_HEADERS";
    public const string LogsHeadersKey = "OTEL_EXPORTER_OTLP_LOGS_HEADERS";

    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration[EndpointKey]);
    }

    public static OtlpLogExportSettings? FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var endpoint = configuration[EndpointKey];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        var logsEndpoint = configuration[LogsEndpointKey];
        return new OtlpLogExportSettings(
            endpoint.Trim(),
            string.IsNullOrWhiteSpace(logsEndpoint) ? null : logsEndpoint.Trim(),
            ParseProtocol(First(configuration[LogsProtocolKey], configuration[ProtocolKey])),
            ParseHeaders(First(configuration[LogsHeadersKey], configuration[HeadersKey])));
    }

    public static OtlpProtocolKind ParseProtocol(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Equals("grpc", StringComparison.OrdinalIgnoreCase))
        {
            return OtlpProtocolKind.Grpc;
        }

        if (raw.Trim().Equals("http/protobuf", StringComparison.OrdinalIgnoreCase))
        {
            return OtlpProtocolKind.HttpProtobuf;
        }

        throw new InvalidOperationException(
            $"OTEL_EXPORTER_OTLP_PROTOCOL '{raw}' is not supported. Use grpc or http/protobuf.");
    }

    public static IReadOnlyDictionary<string, string> ParseHeaders(string? raw)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return headers;
        }

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                throw new InvalidOperationException(
                    "OTEL_EXPORTER_OTLP_HEADERS must be a comma-separated list of key=value pairs.");
            }

            var key = Uri.UnescapeDataString(part[..separator].Trim());
            var value = Uri.UnescapeDataString(part[(separator + 1)..].Trim());
            headers[key] = value;
        }

        return headers;
    }

    private static string? First(string? preferred, string? fallback)
    {
        return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
    }
}
