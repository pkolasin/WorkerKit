namespace WorkerKit.Samples.Outbound.Sms;

/// <summary>Selects <see cref="DryRunSmsSender"/> or <see cref="TwilioSmsSender"/> from <c>Sms:Provider</c> on each send.</summary>
public sealed class RoutingSmsSender(IServiceProvider services, IOptionsMonitor<SmsOptions> options) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        var provider = options.CurrentValue.Provider;
        if (string.Equals(provider, SmsOptions.Twilio, StringComparison.OrdinalIgnoreCase))
        {
            return services.GetRequiredService<TwilioSmsSender>().SendAsync(message, cancellationToken);
        }

        return services.GetRequiredService<DryRunSmsSender>().SendAsync(message, cancellationToken);
    }
}
