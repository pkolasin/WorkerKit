namespace WorkerKit.Samples.Outbound.Queue;

/// <summary>
/// One outbound item. <see cref="Kind"/> is <c>mqtt</c> or <c>sms</c>.
/// For MQTT, <see cref="Destination"/> is the topic (ignored for Azure IoT Hub, which uses the device topic).
/// For SMS, <see cref="Destination"/> is the recipient.
/// </summary>
public sealed record OutboundMessage(string Kind, string Destination, string Body);
