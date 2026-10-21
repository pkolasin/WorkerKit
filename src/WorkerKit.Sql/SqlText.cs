namespace WorkerKit.Sql;

internal static class SqlText
{
    public static string Quote(string identifier, char open, char close)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        var parts = identifier.Split('.');
        if (parts.Length is 0 or > 2)
        {
            throw new ArgumentException(
                $"Identifier '{identifier}' must be a name or schema.name.",
                nameof(identifier));
        }

        return string.Join(".", parts.Select(part => QuotePart(part, open, close)));
    }

    public static string QuoteTemporary(string name, char open, char close, bool hashPrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!hashPrefix)
        {
            return Quote(name, open, close);
        }

        if (name[0] != '#' || name.Length < 2 || !IsSimple(name[1..]))
        {
            throw new ArgumentException(
                "A SQL Server temporary table name starts with '#' and a letter or underscore.",
                nameof(name));
        }

        return string.Concat(open, name, close);
    }

    public static string Parameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (trimmed[0] == '@')
        {
            trimmed = trimmed[1..];
        }

        if (!IsSimple(trimmed))
        {
            throw new ArgumentException(
                $"Parameter '{name}' must be letters, digits, and underscores, and must not start with a digit.",
                nameof(name));
        }

        return "@" + trimmed;
    }

    public static string NormalizeSchemaName(string schemaName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        var trimmed = schemaName.Trim();
        if (trimmed.Length > 200)
        {
            throw new ArgumentException("Schema name must be 200 characters or fewer.", nameof(schemaName));
        }

        return trimmed;
    }

    private static string QuotePart(string part, char open, char close)
    {
        if (!IsSimple(part))
        {
            throw new ArgumentException(
                $"Identifier '{part}' must be letters, digits, and underscores, and must not start with a digit.",
                nameof(part));
        }

        var escaped = part.Replace(close.ToString(), new string(close, 2), StringComparison.Ordinal);
        return string.Concat(open, escaped, close);
    }

    private static bool IsSimple(string value)
    {
        if (value.Length == 0 || !char.IsAsciiLetter(value[0]) && value[0] != '_')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
