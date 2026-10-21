namespace WorkerKit.Sql;

/// <summary>Name and CLR type of one column in a temporary staging table.</summary>
public sealed class BulkColumnDefinition
{
    /// <summary>Stores <paramref name="name"/> and the underlying type of <paramref name="clrType"/>.</summary>
    /// <param name="name">Column name.</param>
    /// <param name="clrType">CLR type. <see cref="Nullable{T}"/> is stored as <c>T</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="clrType"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public BulkColumnDefinition(string name, Type clrType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clrType);
        Name = name;
        ClrType = Nullable.GetUnderlyingType(clrType) ?? clrType;
    }

    /// <summary>Column name.</summary>
    public string Name { get; }

    /// <summary>CLR type used to choose the provider type. Nullable value types are unwrapped.</summary>
    public Type ClrType { get; }
}
