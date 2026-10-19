using Contoso.Billing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkerKit.Hosting.Workers;
using WorkerKit.Testing;

namespace Contoso.Billing.Tests;

public sealed class SamplePollingWorkerTests
{
    [Fact]
    public async Task Item_id_calls_the_sample_client_once()
    {
        var client = new FakeSampleApiClient("payload");
        await using var tester = WorkerAppTester.Create<SamplePollingWorker>(options => options.EnvPrefix = "CONTOSOBILLING")
            .ConfigureApp(app => app.ConfigureHost(builder => builder.AddContosoBillingConnector()))
            .UseSetting("Sample:ItemId", "demo")
            .ConfigureServices(services =>
            {
                services.RemoveAll<ISampleApiClient>();
                services.AddSingleton<ISampleApiClient>(client);
            })
            .Build();

        var result = await tester.RunIterationAsync<SamplePollingWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, result.Status);
        Assert.Equal(1, result.ItemCount);
        Assert.Equal(["demo"], client.Ids);
    }

    private sealed class FakeSampleApiClient(string body) : ISampleApiClient
    {
        public List<string> Ids { get; } = [];

        public Task<string> GetAsync(string id, CancellationToken cancellationToken)
        {
            Ids.Add(id);
            return Task.FromResult(body);
        }
    }
}
