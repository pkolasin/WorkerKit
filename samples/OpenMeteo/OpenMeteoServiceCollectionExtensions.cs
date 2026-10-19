using WorkerKit.Hosting;
using WorkerKit.Hosting.Http;

namespace WorkerKit.Samples.OpenMeteo;

public static class OpenMeteoServiceCollectionExtensions
{
    public static WorkerAppBuilder AddOpenMeteo(this WorkerAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.ConfigureHost(static builder => builder.AddOpenMeteo());
        return app;
    }

    public static IHostApplicationBuilder AddOpenMeteo(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddResilientJsonClient<IOpenMeteoClient, OpenMeteoClient, OpenMeteoOptions>(
            builder.Configuration.GetSection(OpenMeteoOptions.SectionName));
        builder.Services.AddHostedService<ForecastWorker>();
        return builder;
    }
}
