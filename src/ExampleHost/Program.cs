using ExampleHost.Composition;
using WorkerKit.Hosting;

namespace ExampleHost;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = WorkerApp.CreateBuilder<Program>(args);
        app.AddExampleConnector();
        return await app.RunAsync();
    }
}
