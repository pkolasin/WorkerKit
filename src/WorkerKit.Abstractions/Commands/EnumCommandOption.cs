namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// An option whose value is one named member of <typeparamref name="TEnum"/>.
/// Parsing is case-insensitive. A flags enum does not accept combinations: the token must be a single defined member,
/// so <c>Read, Write</c> and an unnamed number are rejected.
/// </summary>
/// <typeparam name="TEnum">Enumeration type. Must be a non-nullable value type.</typeparam>
public sealed class EnumCommandOption<TEnum> : CommandOption<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Creates an option that reads one named <typeparamref name="TEnum"/> member.</summary>
    /// <param name="name">Option name.</param>
    /// <param name="description">Text that describes the option.</param>
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
/// <typeparam name="TEnum">Enumeration type. Must be a non-nullable value type.</typeparam>
public sealed class EnumArrayCommandOption<TEnum> : CommandOption<TEnum[]>
    where TEnum : struct, Enum
{
    /// <summary>Creates an option that reads a list of named <typeparamref name="TEnum"/> members.</summary>
    /// <param name="name">Option name.</param>
    /// <param name="description">Text that describes the option.</param>
    public EnumArrayCommandOption(string name, string description)
        : base(name, description)
    {
        UseReader(EnumTokens.ParseMany<TEnum>);
    }
}
