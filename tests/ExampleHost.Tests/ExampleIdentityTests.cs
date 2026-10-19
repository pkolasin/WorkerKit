using ExampleHost.Composition;
using WorkerKit.Abstractions.Identity;

namespace ExampleHost.Tests;

public sealed class ExampleIdentityTests
{
    [Fact]
    public void Example_assembly_uses_the_workerkit_example_identity()
    {
        var identity = WorkerAppIdentity.FromAssembly(typeof(ExampleConnectorExtensions).Assembly);

        Assert.Equal("WorkerKitExample", identity.Name);
        Assert.Equal("WorkerKitExample", identity.ServiceName);
        Assert.Equal("WORKERKITEXAMPLE", identity.EnvPrefix);
        Assert.Equal("1.0.0", identity.Version);
        Assert.Equal("ExampleHost", identity.ApplicationAssembly.GetName().Name);
        Assert.Equal("WorkerKitExample", identity.ServiceNameFor("default"));
        Assert.Equal("WorkerKitExample-ares", identity.ServiceNameFor("ares"));
        Assert.Equal("worker-kit-example", WorkerAppIdentity.ToKebabCase(identity.Name));
        Assert.Equal("/var/lib/worker-kit-example", identity.LinuxInstancesRoot);
    }
}
