namespace WorkerKit.Samples.Outbound.Mqtt;

public sealed class MqttOptionsValidator : IValidateOptions<MqttOptions>
{
    public ValidateOptionsResult Validate(string? name, MqttOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            errors.Add("Mqtt:Host is required.");
        }

        var azure = string.Equals(options.Provider, MqttOptions.AzureIoTHub, StringComparison.OrdinalIgnoreCase);
        var mosquitto = string.Equals(options.Provider, MqttOptions.Mosquitto, StringComparison.OrdinalIgnoreCase);
        if (!azure && !mosquitto)
        {
            errors.Add("Mqtt:Provider must be Mosquitto or AzureIoTHub.");
        }

        if (mosquitto)
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                errors.Add("Mqtt:ClientId is required when Mqtt:Provider is Mosquitto.");
            }

            if (string.IsNullOrWhiteSpace(options.Topic))
            {
                errors.Add("Mqtt:Topic is required when Mqtt:Provider is Mosquitto.");
            }
        }

        if (azure)
        {
            if (string.IsNullOrWhiteSpace(options.DeviceId))
            {
                errors.Add("Mqtt:DeviceId is required when Mqtt:Provider is AzureIoTHub.");
            }

            if (string.IsNullOrWhiteSpace(options.DeviceSecret))
            {
                errors.Add("Mqtt:DeviceSecret is required when Mqtt:Provider is AzureIoTHub.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
