using System.Collections;

namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// Name, description, options, and positional arguments of a connector command.
/// The tree is built from this object. It does not reference System.CommandLine.
/// </summary>
/// <example>
/// <code>
/// <![CDATA[
/// static readonly CommandOption<string> Id = new("--id", "Record id.");
///
/// var spec = new CommandSpec("lookup", "Look up one record.") { Id };
/// spec.Add(new CommandOption<bool>("--verbose", "Write extra detail."));
/// ]]>
/// </code>
/// </example>
public sealed class CommandSpec : IEnumerable<CommandParameter>
{
    private readonly List<CommandParameter> _parameters = [];

    /// <summary>Creates a command with a name, description, and optional aliases.</summary>
    /// <param name="name">Command name. It cannot be empty, white space, or start with <c>-</c>.</param>
    /// <param name="description">Command description. It cannot be empty or white space.</param>
    /// <param name="aliases">Extra names. Omitting the parameter passes an empty array.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="description"/>, or <paramref name="aliases"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="description"/> is empty or white space, or <paramref name="name"/> starts with <c>-</c>.</exception>
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

    /// <summary>Command name. The constructor rejects a name that starts with <c>-</c>.</summary>
    public string Name { get; }

    /// <summary>Command description.</summary>
    public string Description { get; }

    /// <summary>Extra names for the command. Each alias is a single token and does not start with '-'.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    internal IReadOnlyList<CommandParameter> Parameters => _parameters;

    /// <summary>Adds <paramref name="option"/> to this command.</summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="option">Option to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    public void Add<T>(CommandOption<T> option)
    {
        ArgumentNullException.ThrowIfNull(option);
        _parameters.Add(option);
    }

    /// <summary>Adds <paramref name="argument"/> to this command.</summary>
    /// <typeparam name="T">Argument value type.</typeparam>
    /// <param name="argument">Argument to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
    public void Add<T>(CommandArgument<T> argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        _parameters.Add(argument);
    }

    IEnumerator<CommandParameter> IEnumerable<CommandParameter>.GetEnumerator() => _parameters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _parameters.GetEnumerator();
}
