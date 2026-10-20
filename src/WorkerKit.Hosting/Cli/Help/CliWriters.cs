using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting;

namespace WorkerKit.Hosting.Cli.Help;

/// <summary>Writes a <see cref="CliCommand"/> tree to a text writer.</summary>
public interface ICliWriter
{
    /// <summary>Writes <paramref name="model"/> to <paramref name="output"/>.</summary>
    /// <param name="model">Command tree to write.</param>
    /// <param name="output">Destination for the text.</param>
    void Write(CliCommand model, TextWriter output);
}

/// <summary>Writes the command tree as plain text, then the host configuration notes.</summary>
public sealed class TextCliWriter : ICliWriter
{
    private readonly WorkerAppIdentity _identity;

    /// <summary>Creates a writer that appends <see cref="HostConfigurationNotes"/> for <paramref name="identity"/>.</summary>
    /// <param name="identity">Identity used in the notes. When null, <see cref="AppIdentity.Current"/> is used.</param>
    public TextCliWriter(WorkerAppIdentity? identity = null)
    {
        _identity = identity ?? AppIdentity.Current;
    }

    /// <summary>
    /// Writes each command path, description, arguments, and options, then
    /// <see cref="HostConfigurationNotes.Heading"/> and <see cref="HostConfigurationNotes.For"/>.
    /// </summary>
    /// <param name="model">Command tree to write.</param>
    /// <param name="output">Destination for the text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="output"/> is null.</exception>
    public void Write(CliCommand model, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(output);
        WriteCommand(model, output);
        output.WriteLine();
        output.WriteLine(HostConfigurationNotes.Heading);
        output.WriteLine();
        output.WriteLine(HostConfigurationNotes.For(_identity).Trim());
        output.WriteLine();
    }

    private static void WriteCommand(CliCommand command, TextWriter output)
    {
        output.WriteLine(command.Path);
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            output.WriteLine("  " + command.Description);
        }

        WriteArguments(command.Arguments, output);
        WriteOptions(command.Options, output);
        foreach (var subcommand in command.Subcommands)
        {
            output.WriteLine();
            WriteCommand(subcommand, output);
        }
    }

    private static void WriteOptions(IReadOnlyList<CliOption> options, TextWriter output)
    {
        if (options.Count == 0)
        {
            return;
        }

        output.WriteLine("  Options:");
        foreach (var option in options)
        {
            output.WriteLine("    " + option.Name);
            if (!string.IsNullOrWhiteSpace(option.Description))
            {
                output.WriteLine("      " + option.Description);
            }

            output.WriteLine("      Aliases: " + FormatAliases(option.Aliases));
            output.WriteLine("      Required: " + (option.Required ? "yes" : "no"));
            if (option.DefaultValue is not null)
            {
                output.WriteLine("      Default: " + option.DefaultValue);
            }

            if (option.IsFlag)
            {
                output.WriteLine("      Flag: yes");
            }

            if (option.Recursive)
            {
                output.WriteLine("      Recursive: yes");
            }
        }
    }

    private static void WriteArguments(IReadOnlyList<CliArgument> arguments, TextWriter output)
    {
        if (arguments.Count == 0)
        {
            return;
        }

        output.WriteLine("  Arguments:");
        foreach (var argument in arguments)
        {
            output.WriteLine("    " + argument.Name);
            if (!string.IsNullOrWhiteSpace(argument.Description))
            {
                output.WriteLine("      " + argument.Description);
            }

            output.WriteLine("      Arity: " + FormatArity(argument.MinValues, argument.MaxValues));
            if (argument.DefaultValue is not null)
            {
                output.WriteLine("      Default: " + argument.DefaultValue);
            }
        }
    }

    internal static string FormatAliases(IReadOnlyList<string> aliases) =>
        aliases.Count == 0 ? "(none)" : string.Join(", ", aliases);

    internal static string FormatArity(int min, int max)
    {
        if (max == ArgumentArity.ZeroOrMore.MaximumNumberOfValues)
        {
            return $"{min}..*";
        }

        return $"{min}..{max}";
    }
}

/// <summary>Writes the command tree as Markdown, then the host configuration notes.</summary>
public sealed class MarkdownCliWriter : ICliWriter
{
    private readonly WorkerAppIdentity _identity;

    /// <summary>Creates a writer that appends <see cref="HostConfigurationNotes"/> for <paramref name="identity"/>.</summary>
    /// <param name="identity">Identity used in the notes. When null, <see cref="AppIdentity.Current"/> is used.</param>
    public MarkdownCliWriter(WorkerAppIdentity? identity = null)
    {
        _identity = identity ?? AppIdentity.Current;
    }

    /// <summary>
    /// Writes the command tree as Markdown, including a generated-file note, then
    /// <see cref="HostConfigurationNotes.Heading"/> and <see cref="HostConfigurationNotes.For"/>.
    /// </summary>
    /// <param name="model">Command tree to write.</param>
    /// <param name="output">Destination for the Markdown.</param>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="output"/> is null.</exception>
    public void Write(CliCommand model, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(output);
        output.WriteLine("<!-- Generated by `dotnet run --no-launch-profile -- help --markdown`. The command tree is produced from the root command. Edit HostConfigurationNotes for the overrides section. -->");
        output.WriteLine();
        WriteCommand(model, output, root: true);
        output.WriteLine("## " + HostConfigurationNotes.Heading);
        output.WriteLine();
        output.WriteLine(HostConfigurationNotes.For(_identity).Trim());
        output.WriteLine();
    }

    private static void WriteCommand(CliCommand command, TextWriter output, bool root)
    {
        if (root)
        {
            output.WriteLine("# " + command.Path);
        }
        else
        {
            output.WriteLine("## `" + command.Path + "`");
        }

        output.WriteLine();
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            output.WriteLine(command.Description);
            output.WriteLine();
        }

        if (command.Arguments.Count > 0)
        {
            output.WriteLine("### Arguments");
            output.WriteLine();
            foreach (var argument in command.Arguments)
            {
                output.WriteLine("#### `<" + argument.Name + ">`");
                output.WriteLine();
                if (!string.IsNullOrWhiteSpace(argument.Description))
                {
                    output.WriteLine(argument.Description);
                    output.WriteLine();
                }

                output.WriteLine("- Arity: `" + TextCliWriter.FormatArity(argument.MinValues, argument.MaxValues) + "`");
                output.WriteLine("- Default: " + (argument.DefaultValue ?? "(none)"));
                output.WriteLine();
            }
        }

        if (command.Options.Count > 0)
        {
            output.WriteLine("### Options");
            output.WriteLine();
            foreach (var option in command.Options)
            {
                output.WriteLine("#### `" + option.Name + "`");
                output.WriteLine();
                if (!string.IsNullOrWhiteSpace(option.Description))
                {
                    output.WriteLine(option.Description);
                    output.WriteLine();
                }

                output.WriteLine("- Aliases: " + TextCliWriter.FormatAliases(option.Aliases));
                output.WriteLine("- Required: " + (option.Required ? "yes" : "no"));
                output.WriteLine("- Default: " + (option.DefaultValue ?? "(none)"));
                if (option.IsFlag)
                {
                    output.WriteLine("- Flag: yes");
                }

                if (option.Recursive)
                {
                    output.WriteLine("- Recursive: yes");
                }

                output.WriteLine();
            }
        }

        foreach (var subcommand in command.Subcommands)
        {
            WriteCommand(subcommand, output, root: false);
        }
    }
}
