namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A named command option. Supported values are <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>,
/// <see cref="long"/>, <see cref="double"/>, <see cref="decimal"/>, <see cref="TimeSpan"/>, <see cref="DateOnly"/>,
/// <see cref="DateTimeOffset"/>, <see cref="System.IO.FileInfo"/>, <see cref="System.IO.DirectoryInfo"/>,
/// and arrays of those. Enums use <see cref="EnumCommandOption{TEnum}"/> or <see cref="EnumArrayCommandOption{TEnum}"/>.
/// A flags enum accepts one named member; combined values are rejected.
/// A <see cref="bool"/> option is a flag. An unsupported <typeparamref name="T"/> fails when the command is registered.
/// </summary>
/// <typeparam name="T">Value type. Supported types are listed in the summary.</typeparam>
/// <example>
/// <code>
/// <![CDATA[
/// static readonly CommandOption<string> Id = new("--id", "Record id.") { Required = true };
/// ]]>
/// </code>
/// </example>
public class CommandOption<T> : CommandValue<T>
{
    /// <summary>Creates an option with the given name and description.</summary>
    /// <param name="name">Option name. It must start with <c>-</c>.</param>
    /// <param name="description">Text that describes the option.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> does not start with <c>-</c>.</exception>
    public CommandOption(string name, string description)
        : base(name, description)
    {
        if (!name.StartsWith('-'))
        {
            throw new ArgumentException("An option name must start with '-'.", nameof(name));
        }
    }

    /// <summary>Additional names that refer to this option.</summary>
    /// <remarks>
    /// The initializer is an empty list.
    /// Binding rejects an alias that is <see langword="null"/>, white space, or does not start with <c>-</c>.
    /// </remarks>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>Whether the option must be supplied.</summary>
    /// <remarks>The property has no initializer, so the value is <see langword="false"/>.</remarks>
    public bool Required { get; init; }

    internal override void Bind(CommandSymbolBinder binder)
    {
        EnsureDefaults();
        foreach (var alias in Aliases)
        {
            if (string.IsNullOrWhiteSpace(alias) || !alias.StartsWith('-'))
            {
                throw new InvalidOperationException($"Option alias '{alias}' on {Name} must start with '-'.");
            }
        }

        binder.BindOption(this);
    }
}
