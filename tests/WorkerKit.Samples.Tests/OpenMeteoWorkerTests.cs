using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WorkerKit.Hosting.Workers;
using WorkerKit.Samples.OpenMeteo;
using WorkerKit.Testing;

namespace WorkerKit.Samples.Tests;

public sealed class OpenMeteoWorkerTests
{
    [Fact]
    public async Task Missing_coordinates_do_not_call_the_network()
    {
        var stub = new StubHttpMessageHandler(_ => throw new InvalidOperationException("network"));
        await using var tester = Start(stub);

        var result = await tester.RunIterationAsync<ForecastWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, result.Status);
        Assert.Equal(0, result.ItemCount);
        Assert.Empty(stub.Requests);
    }

    [Fact]
    public async Task Configured_coordinates_request_the_current_temperature()
    {
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"current":{"time":"2026-10-19T12:00","temperature_2m":12.5}}""",
                Encoding.UTF8,
                "application/json"),
        });
        await using var tester = Start(stub, latitude: "52.52", longitude: "13.41");

        var result = await tester.RunIterationAsync<ForecastWorker>();

        Assert.Equal(WorkerIterationStatus.Succeeded, result.Status);
        Assert.Equal(1, result.ItemCount);
        var request = Assert.Single(stub.Requests);
        Assert.Contains("latitude=52.52", request.RequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("longitude=13.41", request.RequestUri?.Query, StringComparison.Ordinal);
        Assert.Contains("current=temperature_2m", request.RequestUri?.Query, StringComparison.Ordinal);
        Assert.Equal("WorkerKitExample/1.0", request.Headers.UserAgent.ToString());
    }

    private static WorkerAppTester Start(StubHttpMessageHandler stub, string? latitude = null, string? longitude = null)
    {
        var builder = WorkerAppTester.Create<ForecastWorker>()
            .ConfigureApp(app => app.AddOpenMeteo())
            .ConfigureServices(services =>
            {
                services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => stub);
            });
        if (latitude is not null)
        {
            builder.UseSetting("OpenMeteo:Latitude", latitude);
        }

        if (longitude is not null)
        {
            builder.UseSetting("OpenMeteo:Longitude", longitude);
        }

        return builder.Build();
    }
}
