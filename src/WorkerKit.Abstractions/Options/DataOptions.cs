namespace WorkerKit.Abstractions.Options;

/// <summary>
/// Optional persistence. <see cref="Provider"/> <c>None</c> skips the database probe.
/// <c>Dapper</c> requires the connector to register <c>IDbConnectionFactory</c>.
/// </summary>
public sealed class DataOptions
{
    public const string SectionName = "Data";

    public string Provider { get; set; } = "None";

    public string ConnectionString { get; set; } = "";
}
