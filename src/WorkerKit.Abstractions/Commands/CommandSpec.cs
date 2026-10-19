using System.Collections;

namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// Name, description, options, and positional arguments of a connector command.
/// The tree is built from this object. It does not reference System.CommandLine.
/// </summary>
public sealed class CommandSpec : IEnumerable<CommandParameter>
{
    private readonly List<CommandParameter> _parameters = [];

    public CommandSpec(string name, string description, params string[] aliases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(aliases);
        if (name.StartsWith('-'))
        {
            throw new ArgumentException("A command name cannot start with '-'.", nameof(name));
        }

        Name = name;
        Description = description;
        Aliases = aliases;
    }

    public string Name { get; }

    public string Description { get; }

    /// <summary>Extra names for the command. Each alias is a single token and does not start with '-'.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    internal IReadOnlyList<CommandParameter> Parameters => _parameters;

    public void Add<T>(CommandOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        _parameters.Add(option);
    }

    public void Add<T>(CommandArgument<T> argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        _parameters.Add(argument);
    }

    IEnumerator<CommandParameter> IEnumerable<CommandParameter>.GetEnumerator() => _parameters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _parameters.GetEnumerator();
}
