using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkerKit.Abstractions.Queues;
using WorkerKit.Hosting.Queues;
using WorkerKit.Samples.Outbound;
using WorkerKit.Samples.Outbound.Mqtt;
using WorkerKit.Samples.Outbound.Queue;
using WorkerKit.Samples.Outbound.Sms;
using WorkerKit.Testing;

namespace WorkerKit.Samples.Tests;

public sealed class OutboundQueueTests
{
    [Fact]
    public async Task Mqtt_item_is_published_to_the_destination_topic()
    {
        var mqtt = new RecordingMqtt();
        var sms = new RecordingSms();
        await using var tester = Start(mqtt, sms, "mqtt", "sensors/temp", "12.5");

        await tester.RunBatchAsync<OutboundQueueWorker>();

        Assert.Equal(("sensors/temp", "12.5"), mqtt.Messages[0]);
        Assert.Empty(sms.Messages);
        Assert.Equal(0, InMemoryQueue.Pending<OutboundMessage>(tester.Services));
    }

    [Fact]
    public async Task Sms_item_uses_the_sender_and_dry_run_is_the_default()
    {
        var mqtt = new RecordingMqtt();
        await using var tester = WorkerAppTester.Create<OutboundQueueWorker>()
            .ConfigureApp(app => app.AddOutbound())
            .UseSetting("Queue:outbound-queue:Enabled", "true")
            .UseSetting("Queue:outbound-queue:ExampleKind", "sms")
            .UseSetting("Queue:outbound-queue:ExampleDestination", "+15551212")
            .UseSetting("Queue:outbound-queue:ExampleBody", "hello")
            .UseSetting("Queue:outbound-queue:MinIdleDelay", "00:00:00.200")
            .UseSetting("Queue:outbound-queue:MaxIdleDelay", "00:00:00.200")
            .ConfigureServices(services =>
            {
                services.RemoveAll<IMqttPublisher>();
                services.AddSingleton<IMqttPublisher>(new RecordingMqtt());
            })
            .Build();

        Assert.IsType<RoutingSmsSender>(tester.Services.GetRequiredService<ISmsSender>());
        Assert.Equal(SmsOptions.DryRun, tester.Services.GetRequiredService<IOptionsMonitor<SmsOptions>>().CurrentValue.Provider);

        await tester.RunBatchAsync<OutboundQueueWorker>();

        Assert.Equal(0, InMemoryQueue.Pending<OutboundMessage>(tester.Services));
        Assert.Empty(InMemoryQueue.DeadLetters<OutboundMessage>(tester.Services));
    }

    [Fact]
    public async Task Unknown_kind_is_dead_lettered()
    {
        var mqtt = new RecordingMqtt();
        var sms = new RecordingSms();
        await using var tester = Start(mqtt, sms, "email", "someone@example.com", "hello");

        await tester.RunBatchAsync<OutboundQueueWorker>();

        Assert.Empty(mqtt.Messages);
        Assert.Empty(sms.Messages);
        Assert.Contains("email", InMemoryQueue.DeadLetters<OutboundMessage>(tester.Services)[0].Reason, StringComparison.Ordinal);
    }

    private static WorkerAppTester Start(RecordingMqtt mqtt, RecordingSms sms, string kind, string destination, string body)
    {
        return WorkerAppTester.Create<OutboundQueueWorker>()
            .ConfigureApp(app => app.AddOutbound())
            .UseSetting("Queue:outbound-queue:Enabled", "true")
            .UseSetting("Queue:outbound-queue:ExampleKind", kind)
            .UseSetting("Queue:outbound-queue:ExampleDestination", destination)
            .UseSetting("Queue:outbound-queue:ExampleBody", body)
            .UseSetting("Queue:outbound-queue:MinIdleDelay", "00:00:00.200")
            .UseSetting("Queue:outbound-queue:MaxIdleDelay", "00:00:00.200")
            .ConfigureServices(services =>
            {
                services.RemoveAll<IMqttPublisher>();
                services.AddSingleton<IMqttPublisher>(mqtt);
                services.RemoveAll<ISmsSender>();
                services.AddSingleton<ISmsSender>(sms);
            })
            .Build();
    }

    private sealed class RecordingMqtt : IMqttPublisher
    {
        public List<(string Topic, string Payload)> Messages { get; } = [];

        public Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
        {
            Messages.Add((topic, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSms : ISmsSender
    {
        public List<SmsMessage> Messages { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
