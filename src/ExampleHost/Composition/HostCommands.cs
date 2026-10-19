using WorkerKit.Hosting;

namespace ExampleHost.Composition;

/// <summary>
/// Command composition for this host. Built-in commands come from <see cref="WorkerApp.CreateBuilder{TProgram}"/>.
/// The VIES sample adds <c>check-vat</c>.
/// </summary>
public static class HostCommands
{
    public static WorkerCommands Create(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return WorkerApp.CreateBuilder<ExampleHost.Program>(args).AddExampleConnector().Commands;
    }
}
