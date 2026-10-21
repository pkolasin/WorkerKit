namespace WorkerKit.Sql;

internal static class SqlTypeNames
{
    public static string Format(Type clrType, bool sqlServer)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (type == typeof(string))
        {
            return sqlServer ? "nvarchar(max)" : "text";
        }

        if (type == typeof(int))
        {
            return sqlServer ? "int" : "integer";
        }

        if (type == typeof(long))
        {
            return "bigint";
        }

        if (type == typeof(short) || type == typeof(byte))
        {
            return sqlServer ? (type == typeof(byte) ? "tinyint" : "smallint") : "smallint";
        }

        if (type == typeof(bool))
        {
            return sqlServer ? "bit" : "boolean";
        }

        if (type == typeof(DateTime))
        {
            return sqlServer ? "datetime2" : "timestamp";
        }

        if (type == typeof(DateTimeOffset))
        {
            return sqlServer ? "datetimeoffset" : "timestamptz";
        }

        if (type == typeof(DateOnly))
        {
            return "date";
        }

        if (type == typeof(Guid))
        {
            return sqlServer ? "uniqueidentifier" : "uuid";
        }

        if (type == typeof(decimal))
        {
            return sqlServer ? "decimal(38, 12)" : "numeric";
        }

        if (type == typeof(double))
        {
            return sqlServer ? "float" : "double precision";
        }

        if (type == typeof(float))
        {
            return "real";
        }

        if (type == typeof(byte[]))
        {
            return sqlServer ? "varbinary(max)" : "bytea";
        }

        throw new ArgumentException(
            $"CLR type '{type.FullName}' has no temporary-table mapping. Supported types are string, integers, bool, DateTime, DateTimeOffset, DateOnly, Guid, decimal, double, float, and byte[].",
            nameof(clrType));
    }
}
