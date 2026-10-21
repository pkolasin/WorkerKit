namespace WorkerKit.Sql;

/// <summary>Maps one property of <typeparamref name="T"/> onto a staging column.</summary>
/// <typeparam name="T">Row type. A Dapper result type is a class with settable properties; this map does not have to be one.</typeparam>
public sealed class BulkColumn<T>
{
    /// <summary>Stores the column name, the CLR type, and the value selector.</summary>
    /// <param name="name">Column name.</param>
    /// <param name="clrType">CLR type of the value. <see cref="Nullable{T}"/> is stored as <c>T</c>.</param>
    /// <param name="value">Reads the column value from one row. Null is written as a database null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="clrType"/>, or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public BulkColumn(string name, Type clrType, Func<T, object?> value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        ClrType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        Value = value;
    }

    /// <summary>Column name.</summary>
    public string Name { get; }

    /// <summary>CLR type used to choose the provider type.</summary>
    public Type ClrType { get; }

    /// <summary>Reads the column value from one row.</summary>
    public Func<T, object?> Value { get; }

    /// <summary>Definition used to create the temporary table.</summary>
    public BulkColumnDefinition Definition => new(Name, ClrType);

    /// <summary>Builds a column from a typed selector.</summary>
    /// <typeparam name="TValue">CLR type of the column.</typeparam>
    /// <param name="name">Column name.</param>
    /// <param name="value">Reads the column value from one row.</param>
    /// <returns>The column map.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public static BulkColumn<T> For<TValue>(string name, Func<T, TValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new BulkColumn<T>(name, typeof(TValue), row => value(row));
    }
}
