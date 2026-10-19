namespace WorkerKit.Abstractions.Commands;

/// <summary>An option or positional argument on a <see cref="CommandSpec"/>.</summary>
public abstract class CommandParameter
{
    protected CommandParameter(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Name = name;
        Description = description;
    }

    public string Name { get; }

    public string Description { get; }

    internal abstract void Bind(CommandSymbolBinder binder);
}

/// <summary>Hosting implements this to turn a spec into a command-line symbol. Not part of the connector API.</summary>
internal abstract class CommandSymbolBinder
{
    public abstract void BindOption<T>(CommandOption<T> option);

    public abstract void BindArgument<T>(CommandArgument<T> argument);
}
