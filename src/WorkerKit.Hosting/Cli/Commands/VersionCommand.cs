using WorkerKit.Abstractions.Identity;
using WorkerKit.Hosting.Cli;
using WorkerKit.Hosting.Cli.Handlers;
using WorkerKit.Hosting;

namespace WorkerKit.Hosting.Cli.Commands;

internal static class VersionCommand
{
    public static Command Create(WorkerAppIdentity? identity)
    {
        var application = identity ?? AppIdentity.Current;
        var command = new Command("version", "Print the host version.");
        // Version is read from the entry assembly. Building the host would resolve the instance
        // and create its directories, which this command does not need.
        command.SetAction(parseResult =>
        {
            var output = parseResult.InvocationConfiguration.Output ?? Console.Out;
            output.WriteLine(VersionHandler.Describe(application));
            return ExitCodes.Success;
        });
        return command;
    }
}
