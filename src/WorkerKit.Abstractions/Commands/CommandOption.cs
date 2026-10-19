namespace WorkerKit.Abstractions.Commands;

/// <summary>
/// A named command option. Supported values are <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>,
/// <see cref="long"/>, <see cref="double"/>, <see cref="decimal"/>, <see cref="TimeSpan"/>, <see cref="DateOnly"/>,
/// <see cref="DateTimeOffset"/>, <see cref="System.IO.FileInfo"/>, <see cref="System.IO.DirectoryInfo"/>,
/// and arrays of those. Enums use <see cref="EnumCommandOption{TEnum}"/> or <see cref="EnumArrayCommandOption{TEnum}"/>.
/// A flags enum accepts one named member; combined values are rejected.
/// A <see cref="bool"/> option is a flag. An unsupported <typeparamref name="T"/> fails when the command is registered.
/// </summary>
public class CommandOption<T> : CommandValue<T>
{
    public CommandOption(string name, string description)
        : base(name, description)
    {
        if (!name.StartsWith('-'))
        {
            throw new ArgumentException("An option name must start with '-'.", nameof(name));
        }
    }

    public IReadOnlyList<string> Aliases { get; init; } = [];

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
