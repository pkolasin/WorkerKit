using WorkerKit.Samples.Outbound.Mqtt;

namespace WorkerKit.Samples.Tests;

public sealed class MqttPublishTargetTests
{
    [Fact]
    public void Mosquitto_uses_the_configured_broker()
    {
        var target = MqttPublishTarget.FromOptions(new MqttOptions
        {
            Provider = MqttOptions.Mosquitto,
            Host = "localhost",
            Port = 1883,
            ClientId = "workerkit-sample",
            Topic = "workerkit/samples/outbound",
        }, DateTimeOffset.UnixEpoch);

        Assert.Equal("localhost", target.Host);
        Assert.Equal(1883, target.Port);
        Assert.False(target.UseTls);
        Assert.Equal("workerkit-sample", target.ClientId);
        Assert.Null(target.Username);
        Assert.Null(target.Password);
        Assert.Equal("workerkit/samples/outbound", target.Topic);
    }

    [Fact]
    public void Azure_iot_hub_builds_the_sas_username_and_device_topic()
    {
        const string secret = "dGVzdC1kZXZpY2Uta2V5";
        var expires = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var target = MqttPublishTarget.FromOptions(new MqttOptions
        {
            Provider = "azureiothub",
            Host = "contoso.azure-devices.net",
            Port = 8883,
            UseTls = false,
            DeviceId = "device-1",
            DeviceSecret = secret,
        }, expires);

        Assert.Equal("contoso.azure-devices.net", target.Host);
        Assert.Equal(8883, target.Port);
        Assert.True(target.UseTls);
        Assert.Equal("device-1", target.ClientId);
        Assert.Equal(
            "contoso.azure-devices.net/device-1/?api-version=2021-04-12",
            target.Username);
        Assert.Equal("devices/device-1/messages/events/", target.Topic);
        Assert.Equal(
            "SharedAccessSignature sr=contoso.azure-devices.net%2Fdevices%2Fdevice-1&sig=P68%2BiOsmDAMjzOkziS1cOLDWghcH%2BvV07wIpFKLdR1U%3D&se=1700000000",
            target.Password);
    }
}
