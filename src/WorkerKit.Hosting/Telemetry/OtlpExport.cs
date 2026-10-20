namespace WorkerKit.Hosting.Telemetry;

/// <summary>
/// OTLP wire protocol read from <see cref="OtlpLogExportSettings.LogsProtocolKey"/> or <see cref="OtlpLogExportSettings.ProtocolKey"/>.
/// </summary>
public enum OtlpProtocolKind
{
    /// <summary><c>grpc</c>. <see cref="OtlpLogExportSettings.ParseProtocol"/> also returns this when the value is omitted.</summary>
    Grpc,

    /// <summary><c>http/protobuf</c>.</summary>
    HttpProtobuf,
}

/// <summary>
/// OTLP settings read from the standard <c>OTEL_EXPORTER_OTLP_*</c> configuration keys.
/// Export stays off until <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set. The .NET exporter and the
/// Serilog sink both default to gRPC on port 4317 when the protocol is omitted.
/// </summary>
/// <param name="Endpoint">Trimmed <see cref="EndpointKey"/>.</param>
/// <param name="LogsEndpoint">Trimmed <see cref="LogsEndpointKey"/>, or null when that key is empty.</param>
/// <param name="Protocol">Protocol from <see cref="LogsProtocolKey"/> when it is set, otherwise <see cref="ProtocolKey"/>.</param>
/// <param name="Headers">Headers from <see cref="LogsHeadersKey"/> when it is set, otherwise <see cref="HeadersKey"/>.</param>
public sealed record OtlpLogExportSettings(
    string Endpoint,
    string? LogsEndpoint,
    OtlpProtocolKind Protocol,
    IReadOnlyDictionary<string, string> Headers)
{
    /// <summary>Configuration key <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>. Export stays off until this key has a value.</summary>
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Configuration key <c>OTEL_EXPORTER_OTLP_LOGS_ENDPOINT</c>.</summary>
    public const string LogsEndpointKey = "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT";

    /// <summary>Configuration key <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>.</summary>
    public const string ProtocolKey = "OTEL_EXPORTER_OTLP_PROTOCOL";

    /// <summary>Configuration key <c>OTEL_EXPORTER_OTLP_LOGS_PROTOCOL</c>. It wins over <see cref="ProtocolKey"/> when it is non-white-space.</summary>
    public const string LogsProtocolKey = "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL";

    /// <summary>Configuration key <c>OTEL_EXPORTER_OTLP_HEADERS</c>.</summary>
    public const string HeadersKey = "OTEL_EXPORTER_OTLP_HEADERS";

    /// <summary>Configuration key <c>OTEL_EXPORTER_OTLP_LOGS_HEADERS</c>. It wins over <see cref="HeadersKey"/> when it is non-white-space.</summary>
    public const string LogsHeadersKey = "OTEL_EXPORTER_OTLP_LOGS_HEADERS";

    /// <summary>True when <see cref="EndpointKey"/> has a non-white-space value.</summary>
    /// <param name="configuration">Configuration that holds the OTEL exporter keys.</param>
    /// <returns><see langword="true"/> when the endpoint is set; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration[EndpointKey]);
    }

    /// <summary>
    /// Reads the OTLP keys. Returns null when <see cref="EndpointKey"/> is missing or white space.
    /// A non-white-space logs protocol or logs headers value replaces the shared key.
    /// </summary>
    /// <param name="configuration">Configuration that holds the OTEL exporter keys.</param>
    /// <returns>The settings, or null when export is off.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The protocol or the headers value cannot be parsed.</exception>
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

    /// <summary>
    /// Maps <paramref name="raw"/> to a protocol. Null, white space, and <c>grpc</c> return <see cref="OtlpProtocolKind.Grpc"/>.
    /// <c>http/protobuf</c> returns <see cref="OtlpProtocolKind.HttpProtobuf"/>. The comparison ignores case.
    /// </summary>
    /// <param name="raw">Protocol text, or null when the configuration key is omitted.</param>
    /// <returns>The parsed protocol.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="raw"/> is not <c>grpc</c> or <c>http/protobuf</c>.</exception>
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

    /// <summary>
    /// Splits a comma-separated list of <c>key=value</c> pairs. Keys and values are URI-unescaped.
    /// A repeated key keeps the last value. Null or white space returns an empty dictionary.
    /// </summary>
    /// <param name="raw">Header list, or null when the configuration key is omitted.</param>
    /// <returns>The parsed headers. The dictionary compares keys ordinally.</returns>
    /// <exception cref="InvalidOperationException">An entry has no <c>=</c>, or the key before <c>=</c> is empty.</exception>
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
