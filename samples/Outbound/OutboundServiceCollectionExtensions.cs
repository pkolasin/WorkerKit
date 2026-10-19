using WorkerKit.Hosting;
using WorkerKit.Hosting.DependencyInjection;
using WorkerKit.Samples.Outbound.Mqtt;
using WorkerKit.Samples.Outbound.Queue;
using WorkerKit.Samples.Outbound.Sms;

namespace WorkerKit.Samples.Outbound;

public static class OutboundServiceCollectionExtensions
{
    public static WorkerAppBuilder AddOutbound(this WorkerAppBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.ConfigureHost(static builder => builder.AddOutbound());
        return app;
    }

    public static IHostApplicationBuilder AddOutbound(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var configuration = builder.Configuration;
        var services = builder.Services;
        services.AddValidatedOptions<MqttOptions>(configuration.GetSection(MqttOptions.SectionName));
        services.AddSingleton<IValidateOptions<MqttOptions>, MqttOptionsValidator>();
        services.AddSingleton<IMqttPublisher, MqttOutboundPublisher>();

        services.AddValidatedOptions<SmsOptions>(configuration.GetSection(SmsOptions.SectionName));
        services.AddSingleton<IValidateOptions<SmsOptions>, SmsOptionsValidator>();
        services.AddHttpClient<TwilioSmsSender>(TwilioSmsSender.HttpClientName, (serviceProvider, client) =>
        {
            var timeout = serviceProvider.GetRequiredService<IOptions<SmsOptions>>().Value.TimeoutSeconds;
            client.BaseAddress = new Uri("https://api.twilio.com/");
            client.Timeout = TimeSpan.FromSeconds(timeout);
        });
        services.AddSingleton<DryRunSmsSender>();
        services.AddSingleton<ISmsSender, RoutingSmsSender>();

        services.AddOptions<OutboundQueueOptions>().Bind(configuration.GetSection(OutboundQueueOptions.SectionName));
        services.AddInMemoryQueueWorker<OutboundQueueWorker, OutboundMessage, OutboundQueueHandler>(
            configuration,
            OutboundQueueWorker.WorkerNameValue);
        return builder;
    }
}
