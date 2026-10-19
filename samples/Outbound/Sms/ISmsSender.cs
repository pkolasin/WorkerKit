namespace WorkerKit.Samples.Outbound.Sms;

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}
