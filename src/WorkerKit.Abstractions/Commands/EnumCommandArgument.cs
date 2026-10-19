namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A positional argument whose value is one named member of <typeparamref name="TEnum"/>.
/// Parsing is case-insensitive. A flags enum does not accept combinations: the token must be a single defined member.
/// </summary>
public sealed class EnumCommandArgument<TEnum> : CommandArgument<TEnum>
    where TEnum : struct, Enum
{
    public EnumCommandArgument(string name, string description)
        : base(name, description)
    {
        UseReader(EnumTokens.ParseOne<TEnum>);
    }
}

/// <summary>
/// A positional argument whose value is a list of named members of <typeparamref name="TEnum"/>.
/// Each token is one defined member. Combined flags values are rejected, the same as <see cref="EnumCommandArgument{TEnum}"/>.
/// </summary>
public sealed class EnumArrayCommandArgument<TEnum> : CommandArgument<TEnum[]>
    where TEnum : struct, Enum
{
    public EnumArrayCommandArgument(string name, string description)
        : base(name, description)
    {
        UseReader(EnumTokens.ParseMany<TEnum>);
    }
}
