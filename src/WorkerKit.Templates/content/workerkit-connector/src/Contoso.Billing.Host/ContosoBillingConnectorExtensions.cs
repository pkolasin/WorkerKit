namespace Contoso.Billing;

public static partial class ContosoBillingConnectorExtensions
{
    public static IHostApplicationBuilder AddContosoBillingConnector(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var configuration = builder.Configuration;
        builder.Services.AddValidatedOptions<SampleWorkerOptions>(configuration.GetSection(SampleWorkerOptions.SectionName));
        builder.Services.AddResilientJsonClient<ISampleApiClient, SampleApiClient, SampleApiOptions>(
            configuration.GetSection(SampleApiOptions.SectionName));
        builder.Services.AddHostedService<SamplePollingWorker>();
        builder.Services.AddScoped<SampleHandler>();
        AddSampleQueue(builder);
        return builder;
    }

    static partial void AddSampleQueue(IHostApplicationBuilder builder);
}
