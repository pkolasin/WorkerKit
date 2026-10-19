using MQTTnet;
using MQTTnet.Protocol;

namespace WorkerKit.Samples.Outbound.Mqtt;

/// <summary>
/// Publishes one MQTT message and disconnects. Mosquitto uses the configured topic when the caller passes an empty topic.
/// Azure IoT Hub always publishes to <c>devices/{deviceId}/messages/events/</c>.
/// </summary>
public sealed class MqttOutboundPublisher(IOptionsMonitor<MqttOptions> options, TimeProvider time, ILogger<MqttOutboundPublisher> logger)
    : IMqttPublisher
{
    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var target = MqttPublishTarget.FromOptions(options.CurrentValue, time.GetUtcNow());
        var resolvedTopic = string.IsNullOrWhiteSpace(topic) ? target.Topic : topic.Trim();
        if (string.Equals(options.CurrentValue.Provider, MqttOptions.AzureIoTHub, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTopic = target.Topic;
        }

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(target.Host, target.Port)
            .WithClientId(target.ClientId);
        if (target.Username is not null)
        {
            builder.WithCredentials(target.Username, target.Password ?? "");
        }

        if (target.UseTls)
        {
            builder.WithTlsOptions(tls => tls.UseTls(true));
        }

        logger.LogInformation("Publishing MQTT message to {Host}:{Port} topic {Topic}", target.Host, target.Port, resolvedTopic);
        await client.ConnectAsync(builder.Build(), cancellationToken).ConfigureAwait(false);
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(resolvedTopic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            var result = await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"MQTT publish was rejected ({result.ReasonCode}).");
            }
        }
        finally
        {
            await client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken).ConfigureAwait(false);
        }
    }
}
