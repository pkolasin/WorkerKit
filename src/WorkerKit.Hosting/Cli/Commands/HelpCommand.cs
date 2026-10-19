using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Help;

namespace WorkerKit.Hosting.Cli.Commands;

internal static class HelpCommand
{
    public static Command Create(Func<RootCommand> root, WorkerAppIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(identity);
        var path = new Argument<string[]>("command")
        {
            Description = "Command path, for example: <command>. Empty lists all commands.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var markdown = new Option<bool>("--markdown")
        {
            Description = "Print the full CLI reference as Markdown.",
        };

        var command = new Command("help", "Show help for a command, or list all commands with their options.");
        command.Add(path);
        command.Add(markdown);
        command.SetAction(parseResult =>
        {
            var tokens = parseResult.GetValue(path) ?? [];
            var output = parseResult.InvocationConfiguration.Output ?? Console.Out;
            if (tokens.Length > 0)
            {
                // Same text as "<command> --help", including the caller's output writer.
                return root().Parse([.. tokens, "--help"]).Invoke(parseResult.InvocationConfiguration);
            }

            var model = CliModel.Describe(root(), identity);
            ICliWriter writer = parseResult.GetValue(markdown)
                ? new MarkdownCliWriter(identity)
                : new TextCliWriter(identity);
            writer.Write(model, output);
            return ExitCodes.Success;
        });
        return command;
    }
}
