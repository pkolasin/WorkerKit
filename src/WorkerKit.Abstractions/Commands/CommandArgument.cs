namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A positional argument. The value types are the same set as <see cref="CommandOption{T}"/>.
/// Enums use <see cref="EnumCommandArgument{TEnum}"/> or <see cref="EnumArrayCommandArgument{TEnum}"/>.
/// A flags enum accepts one named member; combined values are rejected.
/// An unsupported <typeparamref name="T"/> fails when the command is registered.
/// </summary>
/// <typeparam name="T">Value type. The supported types are the same set as <see cref="CommandOption{T}"/>.</typeparam>
public class CommandArgument<T> : CommandValue<T>
{
    /// <summary>Creates an argument with the given name and description.</summary>
    /// <param name="name">Argument name. It cannot start with <c>-</c> or contain whitespace.</param>
    /// <param name="description">Text that describes the argument.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> starts with <c>-</c> or contains whitespace.</exception>
    public CommandArgument(string name, string description)
        : base(name, description)
    {
        if (name.StartsWith('-') || name.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("An argument name cannot start with '-' or contain whitespace.", nameof(name));
        }
    }

    /// <summary>Whether the argument must be supplied.</summary>
    /// <remarks>The initializer is <see langword="true"/>.</remarks>
    public bool Required { get; init; } = true;

    internal override void Bind(CommandSymbolBinder binder)
    {
        EnsureDefaults();
        binder.BindArgument(this);
    }
}
