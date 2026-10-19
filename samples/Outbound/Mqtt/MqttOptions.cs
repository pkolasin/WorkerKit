using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Samples.Outbound.Mqtt;

/// <summary>
/// MQTT broker. <see cref="Mosquitto"/> is a local broker on <c>localhost:1883</c> and is the default.
/// <see cref="AzureIoTHub"/> builds the TLS username, SAS password, and device topic from the device key.
/// </summary>
public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public const string Mosquitto = "Mosquitto";

    public const string AzureIoTHub = "AzureIoTHub";

    public string Provider { get; set; } = Mosquitto;

    public string Host { get; set; } = "localhost";

    [Range(1, 65535, ErrorMessage = "Mqtt:Port must be between 1 and 65535.")]
    public int Port { get; set; } = 1883;

    public bool UseTls { get; set; }

    public string ClientId { get; set; } = "workerkit-sample";

    public string Topic { get; set; } = "workerkit/samples/outbound";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string DeviceId { get; set; } = "";

    /// <summary>Base64 device shared access key. The name contains Secret so <c>check-config</c> masks it.</summary>
    public string DeviceSecret { get; set; } = "";

    [Range(1, 1440, ErrorMessage = "Mqtt:SasLifetimeMinutes must be between 1 and 1440.")]
    public int SasLifetimeMinutes { get; set; } = 60;
}
