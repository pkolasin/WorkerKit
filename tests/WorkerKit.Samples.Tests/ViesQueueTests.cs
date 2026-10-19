using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkerKit.Samples.Vies;
using WorkerKit.Samples.Vies.Queue;
using WorkerKit.Testing;

namespace WorkerKit.Samples.Tests;

public sealed class ViesQueueTests
{
    [Fact]
    public async Task Configured_number_is_checked_when_the_queue_is_enabled()
    {
        var checker = new ScriptChecker();
        await using var tester = WorkerAppTester.Create<ViesQueueWorker>()
            .ConfigureApp(app =>
            {
                app.AddVies();
                app.AddViesQueue();
            })
            .UseSetting("Queue:vies-queue:Enabled", "true")
            .UseSetting("Queue:vies-queue:CountryCode", " de ")
            .UseSetting("Queue:vies-queue:VatNumber", "123456789")
            .UseSetting("Queue:vies-queue:MinIdleDelay", "00:00:00.200")
            .UseSetting("Queue:vies-queue:MaxIdleDelay", "00:00:00.200")
            .ConfigureServices(services =>
            {
                services.RemoveAll<IViesVatChecker>();
                services.AddSingleton<IViesVatChecker>(checker);
            })
            .Build();

        await tester.RunBatchAsync<ViesQueueWorker>();

        Assert.Equal(("DE", "123456789"), checker.Calls[0]);
        Assert.Equal(0, InMemoryQueue.Pending<ViesQueuePayload>(tester.Services));
    }

    private sealed class ScriptChecker : IViesVatChecker
    {
        public List<(string Country, string Number)> Calls { get; } = [];

        public Task<ViesVatResult> CheckAsync(string countryCode, string vatNumber, CancellationToken cancellationToken)
        {
            var country = ViesVatNumber.NormalizeCountry(countryCode);
            var number = ViesVatNumber.NormalizeNumber(country, vatNumber);
            Calls.Add((country, number));
            return Task.FromResult(new ViesVatResult(country, number, true, null, null, DateTime.UtcNow));
        }
    }
}
