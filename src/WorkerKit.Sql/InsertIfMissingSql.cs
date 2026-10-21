namespace WorkerKit.Sql;

/// <summary>
/// Insert that stores a row when its key is new.
/// PostgreSQL uses <c>ON CONFLICT DO NOTHING</c>.
/// SQL Server inserts and the caller treats duplicate-key errors 2601 and 2627 as the row already existing.
/// The destination needs a unique constraint on <see cref="KeyColumns"/>.
/// </summary>
public sealed class InsertIfMissingSql
{
    /// <summary>Stores the table, the inserted columns, the conflict key, and the id column.</summary>
    /// <param name="table">Destination table. A schema prefix is <c>schema.table</c>.</param>
    /// <param name="columns">Columns supplied by parameters of the same name. The id column is not one of them.</param>
    /// <param name="keyColumns">Columns of the unique constraint. Each one must also be in <paramref name="columns"/>.</param>
    /// <param name="idColumn">Surrogate key the statement returns. The default is <c>Id</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="table"/>, <paramref name="columns"/>, <paramref name="keyColumns"/>, or <paramref name="idColumn"/> is null.</exception>
    /// <exception cref="ArgumentException">A name is empty, or either list is empty.</exception>
    public InsertIfMissingSql(
        string table,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> keyColumns,
        string idColumn = "Id")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(keyColumns);
        ArgumentException.ThrowIfNullOrWhiteSpace(idColumn);
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one column is required.", nameof(columns));
        }

        if (keyColumns.Count == 0)
        {
            throw new ArgumentException("At least one key column is required.", nameof(keyColumns));
        }

        Table = table;
        Columns = columns;
        KeyColumns = keyColumns;
        IdColumn = idColumn;
    }

    /// <summary>Destination table.</summary>
    public string Table { get; }

    /// <summary>Columns bound as parameters of the same name.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>Unique key used for <c>ON CONFLICT</c> and for the re-read after a duplicate key.</summary>
    public IReadOnlyList<string> KeyColumns { get; }

    /// <summary>Surrogate key column returned as <see cref="long"/>.</summary>
    public string IdColumn { get; }

    /// <summary>Alias of the returned id.</summary>
    /// <remarks>The initializer is <c>Id</c>. PostgreSQL quotes it so the casing is preserved.</remarks>
    public string IdAlias { get; init; } = "Id";
}
