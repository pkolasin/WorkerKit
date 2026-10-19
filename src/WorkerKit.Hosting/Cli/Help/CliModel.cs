using System.CommandLine.Help;
using System.Globalization;
using WorkerKit.Abstractions.Identity;

namespace WorkerKit.Hosting.Cli.Help;

public sealed record CliOption(
    string Name,
    IReadOnlyList<string> Aliases,
    string? Description,
    bool Required,
    bool IsFlag,
    string? DefaultValue,
    bool Recursive);

public sealed record CliArgument(
    string Name,
    string? Description,
    int MinValues,
    int MaxValues,
    string? DefaultValue);

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
