namespace WorkerKit.Abstractions.Commands;

/// <summary>An option or positional argument on a <see cref="CommandSpec"/>.</summary>
public abstract class CommandParameter
{
    /// <summary>Creates a parameter with a name and a description.</summary>
    /// <param name="name">Parameter name. It cannot be <see langword="null"/>, empty, or white space.</param>
    /// <param name="description">Parameter description. It cannot be <see langword="null"/>, empty, or white space.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="description"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="description"/> is empty or white space.</exception>
    protected CommandParameter(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
    }

    /// <summary>Name of the option or argument.</summary>
    public string Name { get; }

    /// <summary>Description of the option or argument.</summary>
    public string Description { get; }

    internal abstract void Bind(CommandSymbolBinder binder);
}

/// <summary>Hosting implements this to turn a spec into a command-line symbol. Not part of the connector API.</summary>
internal abstract class CommandSymbolBinder
{
    public abstract void BindOption<T>(CommandOption<T> option);

    public abstract void BindArgument<T>(CommandArgument<T> argument);
}
