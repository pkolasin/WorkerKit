namespace WorkerKit.Samples.Outbound.Mqtt;

public interface IMqttPublisher
{
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken);
}
