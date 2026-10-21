namespace WorkerKit.Sql;

/// <summary>One forward schema change. Versions start at 1 and are applied in order.</summary>
public sealed class SchemaMigration
{
    /// <summary>Stores <paramref name="version"/> and <paramref name="sql"/>.</summary>
    /// <param name="version">Version number. It is greater than zero.</param>
    /// <param name="sql">Provider SQL. SQL Server scripts may separate batches with a line that is <c>GO</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="sql"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sql"/> is empty or white space.</exception>
    public SchemaMigration(int version, string sql)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Schema version must be at least 1.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        Version = version;
        Sql = sql;
    }

    /// <summary>Version number stored after the script succeeds.</summary>
    public int Version { get; }

    /// <summary>SQL applied while the schema lock is held.</summary>
    public string Sql { get; }
}
