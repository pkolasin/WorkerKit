namespace WorkerKit.Samples.Outbound.Sms;

/// <summary>Logs the SMS and does not call a provider. This is the default <see cref="ISmsSender"/>.</summary>
public sealed class DryRunSmsSender(ILogger<DryRunSmsSender> logger) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Dry-run SMS to {To}: {Body}", message.To, message.Body);
        return Task.CompletedTask;
    }
}
