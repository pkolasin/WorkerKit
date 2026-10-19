namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// An option whose value is one named member of <typeparamref name="TEnum"/>.
/// Parsing is case-insensitive. A flags enum does not accept combinations: the token must be a single defined member,
/// so <c>Read, Write</c> and an unnamed number are rejected.
/// </summary>
public sealed class EnumCommandOption<TEnum> : CommandOption<TEnum>
    where TEnum : struct, Enum
{
    public EnumCommandOption(string name, string description)
        : base(name, description)
    {
        UseReader(EnumTokens.ParseOne<TEnum>);
    }
}

/// <summary>
/// An option whose value is a list of named members of <typeparamref name="TEnum"/>.
/// Each token is one defined member. Combined flags values are rejected, the same as <see cref="EnumCommandOption{TEnum}"/>.
/// </summary>
public sealed class EnumArrayCommandOption<TEnum> : CommandOption<TEnum[]>
    where TEnum : struct, Enum
{
    public EnumArrayCommandOption(string name, string description)
        : base(name, description)
    {
        UseReader(EnumTokens.ParseMany<TEnum>);
    }
}
