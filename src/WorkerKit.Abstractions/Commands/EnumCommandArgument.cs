namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A positional argument whose value is one named member of <typeparamref name="TEnum"/>.
/// Parsing is case-insensitive. A flags enum does not accept combinations: the token must be a single defined member.
/// </summary>
/// <typeparam name="TEnum">Enumeration type. Must be a non-nullable value type.</typeparam>
public sealed class EnumCommandArgument<TEnum> : CommandArgument<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Creates an argument that reads one named <typeparamref name="TEnum"/> member.</summary>
    /// <param name="name">Argument name.</param>
    /// <param name="description">Text that describes the argument.</param>
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
/// <typeparam name="TEnum">Enumeration type. Must be a non-nullable value type.</typeparam>
public sealed class EnumArrayCommandArgument<TEnum> : CommandArgument<TEnum[]>
    where TEnum : struct, Enum
{
    /// <summary>Creates an argument that reads a list of named <typeparamref name="TEnum"/> members.</summary>
    /// <param name="name">Argument name.</param>
    /// <param name="description">Text that describes the argument.</param>
    public EnumArrayCommandArgument(string name, string description)
        : base(name, description)
    {
        UseReader(EnumTokens.ParseMany<TEnum>);
    }
}
