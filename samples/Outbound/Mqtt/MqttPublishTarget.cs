using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WorkerKit.Samples.Outbound.Mqtt;

/// <summary>Connection values for one publish. Azure IoT Hub values are derived; Mosquitto values are copied.</summary>
public sealed record MqttPublishTarget(
    string Host,
    int Port,
    bool UseTls,
    string ClientId,
    string? Username,
    string? Password,
    string Topic)
{
    public const string AzureApiVersion = "2021-04-12";

    public static MqttPublishTarget FromOptions(MqttOptions options, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.Equals(options.Provider, MqttOptions.AzureIoTHub, StringComparison.OrdinalIgnoreCase))
        {
            return ForAzureIoTHub(options, utcNow);
        }

        return new MqttPublishTarget(
            options.Host.Trim(),
            options.Port,
            options.UseTls,
            options.ClientId.Trim(),
            EmptyToNull(options.Username),
            EmptyToNull(options.Password),
            options.Topic.Trim());
    }

    public static MqttPublishTarget ForAzureIoTHub(MqttOptions options, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(options);
        var host = options.Host.Trim();
        var deviceId = options.DeviceId.Trim();
        var resource = host + "/devices/" + deviceId;
        var expiry = utcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Sign(options.DeviceSecret.Trim(), resource + "\n" + expiry);
        var password = "SharedAccessSignature sr="
            + Uri.EscapeDataString(resource)
            + "&sig="
            + Uri.EscapeDataString(signature)
            + "&se="
            + expiry;
        return new MqttPublishTarget(
            host,
            options.Port,
            UseTls: true,
            ClientId: deviceId,
            Username: host + "/" + deviceId + "/?api-version=" + AzureApiVersion,
            Password: password,
            Topic: "devices/" + deviceId + "/messages/events/");
    }

    public static string Sign(string deviceSecretBase64, string resourceAndExpiry)
    {
        var key = Convert.FromBase64String(deviceSecretBase64);
        var bytes = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(resourceAndExpiry));
        return Convert.ToBase64String(bytes);
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
