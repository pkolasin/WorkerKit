using WorkerKit.Hosting;
using WorkerKit.Hosting.DependencyInjection;
using WorkerKit.Hosting.Telemetry;
using WorkerKit.Samples.Vies.Queue;

namespace WorkerKit.Samples.Vies;

public static class ViesServiceCollectionExtensions
{
    public static WorkerAppBuilder AddVies(this WorkerAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Commands.Add<CheckVatCommand>();
        app.ConfigureHost(static builder => builder.AddVies());
        return app;
    }

    public static IHostApplicationBuilder AddVies(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var services = builder.Services;
        builder.AddTelemetrySource(ViesTelemetry.SourceName);
        services.AddValidatedOptions<ViesClientOptions>(builder.Configuration.GetSection(ViesClientOptions.SectionName));
        services.AddSingleton<IValidateOptions<ViesClientOptions>, ViesClientOptionsValidator>();
        services.AddSingleton<IViesVatChecker, ViesVatChecker>();
        return builder;
    }

    public static WorkerAppBuilder AddViesQueue(this WorkerAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.ConfigureHost(static builder => builder.AddViesQueue());
        return app;
    }

    public static IHostApplicationBuilder AddViesQueue(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var configuration = builder.Configuration;
        var services = builder.Services;
        services.AddOptions<ViesQueueOptions>().Bind(configuration.GetSection(ViesQueueOptions.SectionName));
        services.AddInMemoryQueueWorker<ViesQueueWorker, ViesQueuePayload, ViesQueueHandler>(
            configuration,
            ViesQueueWorker.WorkerNameValue);
        return builder;
    }
}
