using System.CommandLine.Help;
using System.Globalization;
using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting.Cli.Help;

/// <summary>One option copied from a command in the tree.</summary>
/// <param name="Name">Option name.</param>
/// <param name="Aliases">Option aliases. Empty when the option has none.</param>
/// <param name="Description">Option description, or null when the option has none.</param>
/// <param name="Required">True when the option must be supplied.</param>
/// <param name="IsFlag">True when the option is a flag.</param>
/// <param name="DefaultValue">Formatted default, or null when the option has no default.</param>
/// <param name="Recursive">True when the option applies to nested commands.</param>
public sealed record CliOption(
    string Name,
    IReadOnlyList<string> Aliases,
    string? Description,
    bool Required,
    bool IsFlag,
    string? DefaultValue,
    bool Recursive);

/// <summary>One argument copied from a command in the tree.</summary>
/// <param name="Name">Argument name.</param>
/// <param name="Description">Argument description, or null when the argument has none.</param>
/// <param name="MinValues">Minimum number of values.</param>
/// <param name="MaxValues">Maximum number of values.</param>
/// <param name="DefaultValue">Formatted default, or null when the argument has no default.</param>
public sealed record CliArgument(
    string Name,
    string? Description,
    int MinValues,
    int MaxValues,
    string? DefaultValue);

/// <summary>One command and its visible options, arguments, and subcommands.</summary>
/// <param name="Path">Command path from the executable name through this command.</param>
/// <param name="Description">Command description, or null when the command has none.</param>
/// <param name="Options">Visible options on this command.</param>
/// <param name="Arguments">Visible arguments on this command.</param>
/// <param name="Subcommands">Visible subcommands.</param>
public sealed record CliCommand(
    string Path,
    string? Description,
    IReadOnlyList<CliOption> Options,
    IReadOnlyList<CliArgument> Arguments,
    IReadOnlyList<CliCommand> Subcommands);

/// <summary>
/// Walks a <see cref="Command"/> tree. Nothing is registered by hand: a new command shows up here
/// as soon as it is added to <c>WorkerCommands</c>.
/// </summary>
public static class CliModel
{
    /// <summary>
    /// Walks <paramref name="command"/>. The root path is the application assembly name, not the
    /// process name (<c>testhost</c> under <c>dotnet test</c>).
    /// </summary>
    /// <param name="command">Command to walk. Hidden commands, options, and arguments are omitted, as are help and version options.</param>
    /// <param name="identity">Application identity. When null, <see cref="AppIdentity.Current"/> is used.</param>
    /// <param name="parentPath">Path of the parent command. Null marks <paramref name="command"/> as the root.</param>
    /// <returns>The command, its visible symbols, and its visible subcommands.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
    public static CliCommand Describe(Command command, WorkerAppIdentity? identity = null, string? parentPath = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        identity ??= AppIdentity.Current;
        var executable = identity.ApplicationAssembly.GetName().Name ?? identity.Name;
        var name = parentPath is null ? executable : command.Name;
        var path = parentPath is null ? name : $"{parentPath} {name}";
        return new CliCommand(
            path,
            command.Description,
            command.Options
                .Where(option => !option.Hidden && option is not HelpOption and not VersionOption)
                .Select(option => new CliOption(
                    option.Name,
                    option.Aliases.ToArray(),
                    option.Description,
                    option.Required,
                    option.ValueType == typeof(bool),
                    option.HasDefaultValue ? Format(option.GetDefaultValue()) : null,
                    option.Recursive))
                .ToArray(),
            command.Arguments
                .Where(argument => !argument.Hidden)
                .Select(argument => new CliArgument(
                    argument.Name,
                    argument.Description,
                    argument.Arity.MinimumNumberOfValues,
                    argument.Arity.MaximumNumberOfValues,
                    argument.HasDefaultValue ? Format(argument.GetDefaultValue()) : null))
                .ToArray(),
            command.Subcommands
                .Where(subcommand => !subcommand.Hidden)
                .Select(subcommand => Describe(subcommand, identity, path))
                .ToArray());
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        string text => text.Length == 0 ? "\"\"" : text,
        System.Collections.IEnumerable values => string.Join(", ", values.Cast<object>()),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture),
    };
}
