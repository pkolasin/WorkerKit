namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A positional argument. The value types are the same set as <see cref="CommandOption{T}"/>.
/// Enums use <see cref="EnumCommandArgument{TEnum}"/> or <see cref="EnumArrayCommandArgument{TEnum}"/>.
/// A flags enum accepts one named member; combined values are rejected.
/// An unsupported <typeparamref name="T"/> fails when the command is registered.
/// </summary>
public class CommandArgument<T> : CommandValue<T>
{
    public CommandArgument(string name, string description)
        : base(name, description)
    {
        if (name.StartsWith('-') || name.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("An argument name cannot start with '-' or contain whitespace.", nameof(name));
        }
    }

    public bool Required { get; init; } = true;

    internal override void Bind(CommandSymbolBinder binder)
    {
        EnsureDefaults();
        binder.BindArgument(this);
    }
}
