namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// Parses one named enum member. Combined flags values and unnamed numbers are rejected.
/// </summary>
internal static class EnumTokens
{
    public static TEnum ParseOne<TEnum>(IReadOnlyList<string> tokens)
        where TEnum : struct, Enum
    {
        if (tokens.Count != 1)
        {
            throw new FormatException($"Expected one {typeof(TEnum).Name} value.");
        }

        return Parse<TEnum>(tokens[0]);
    }

    public static TEnum[] ParseMany<TEnum>(IReadOnlyList<string> tokens)
        where TEnum : struct, Enum
        => tokens.Select(Parse<TEnum>).ToArray();

    public static TEnum Parse<TEnum>(string text)
        where TEnum : struct, Enum
    {
        if (System.Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) && System.Enum.IsDefined(value))
        {
            return value;
        }

        throw new FormatException($"'{text}' is not a {typeof(TEnum).Name} value.");
    }
}
