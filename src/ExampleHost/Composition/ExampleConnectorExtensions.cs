using WorkerKit.Hosting;
using WorkerKit.Samples.OpenMeteo;
using WorkerKit.Samples.Outbound;
using WorkerKit.Samples.Vies;

namespace ExampleHost.Composition;

/// <summary>
/// Sample composition root. A connector replaces this with its own extension:
/// <c>app.Commands.Add&lt;MyCommand&gt;()</c> and <c>app.ConfigureHost(builder =&gt; builder.AddMyConnector())</c>.
/// </summary>
public static class ExampleConnectorExtensions
{
    public static WorkerAppBuilder AddExampleConnector(this WorkerAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.AddVies();
        app.AddOpenMeteo();
        app.AddViesQueue();
        app.AddOutbound();
        return app;
    }

    public static IHostApplicationBuilder AddExampleConnector(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddVies();
        builder.AddOpenMeteo();
        builder.AddViesQueue();
        builder.AddOutbound();
        return builder;
    }
}
