using WorkerKit.Abstractions.Queues;
using WorkerKit.Samples.Outbound.Mqtt;
using WorkerKit.Samples.Outbound.Sms;

namespace WorkerKit.Samples.Outbound.Queue;

public sealed class OutboundQueueHandler(IMqttPublisher mqtt, ISmsSender sms, ILogger<OutboundQueueHandler> logger)
    : IWorkItemHandler<OutboundMessage>
{
    public async Task HandleAsync(WorkItem<OutboundMessage> item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var message = item.Payload;
        if (string.IsNullOrWhiteSpace(message.Destination))
        {
            throw new PermanentWorkItemException("Outbound destination is empty.");
        }

        if (string.Equals(message.Kind, "mqtt", StringComparison.OrdinalIgnoreCase))
        {
            await mqtt.PublishAsync(message.Destination, message.Body, cancellationToken).ConfigureAwait(false);
        }
        else if (string.Equals(message.Kind, "sms", StringComparison.OrdinalIgnoreCase))
        {
            await sms.SendAsync(new SmsMessage(message.Destination, message.Body), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new PermanentWorkItemException($"Outbound kind '{message.Kind}' is not mqtt or sms.");
        }

        logger.LogInformation(
            "Handled outbound item {Id} attempt {Attempt}: {Kind} to {Destination}",
            item.Id,
            item.Attempt,
            message.Kind,
            message.Destination);
    }
}
