using System.Net.Http.Headers;
using System.Text;

namespace WorkerKit.Samples.Outbound.Sms;

/// <summary>
/// Posts one message to <c>https://api.twilio.com/2010-04-01/Accounts/{AccountSid}/Messages.json</c>.
/// </summary>
public sealed class TwilioSmsSender(HttpClient httpClient, IOptionsMonitor<SmsOptions> options, ILogger<TwilioSmsSender> logger)
    : ISmsSender
{
    public const string HttpClientName = "twilio-sms";

    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var settings = options.CurrentValue;
        var account = settings.AccountSid.Trim();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"2010-04-01/Accounts/{Uri.EscapeDataString(account)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = message.To,
                ["From"] = settings.From.Trim(),
                ["Body"] = message.Body,
            }),
        };
        var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes(account + ":" + settings.AuthToken));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credential);
        logger.LogInformation("Sending SMS through Twilio to {To}", message.To);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (body.Length > 500)
            {
                body = body[..500];
            }

            throw new HttpRequestException(
                $"Twilio returned {(int)response.StatusCode} ({response.StatusCode}). {body}",
                inner: null,
                statusCode: response.StatusCode);
        }
    }
}
