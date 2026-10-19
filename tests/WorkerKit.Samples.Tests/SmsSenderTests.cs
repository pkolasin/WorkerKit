using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkerKit.Samples.Outbound.Sms;

namespace WorkerKit.Samples.Tests;

public sealed class SmsSenderTests
{
    [Fact]
    public async Task Dry_run_completes_without_a_network_call()
    {
        var sender = new DryRunSmsSender(NullLogger<DryRunSmsSender>.Instance);

        await sender.SendAsync(new SmsMessage("+15551212", "hello"), CancellationToken.None);
    }

    [Fact]
    public async Task Twilio_posts_form_fields_with_basic_auth()
    {
        string? body = null;
        AuthenticationHeaderValue? authorization = null;
        Uri? uri = null;
        var stub = new StubHttpMessageHandler(request =>
        {
            uri = request.RequestUri;
            authorization = request.Headers.Authorization;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(stub) { BaseAddress = new Uri("https://api.twilio.com/") };
        var sender = new TwilioSmsSender(
            http,
            new FixedOptions(new SmsOptions
            {
                Provider = SmsOptions.Twilio,
                AccountSid = "AC123",
                AuthToken = "token",
                From = "+15550001",
            }),
            NullLogger<TwilioSmsSender>.Instance);

        await sender.SendAsync(new SmsMessage("+15551212", "hello there"), CancellationToken.None);

        Assert.Equal("https://api.twilio.com/2010-04-01/Accounts/AC123/Messages.json", uri?.AbsoluteUri);
        Assert.Equal("Basic", authorization?.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("AC123:token")), authorization?.Parameter);
        Assert.Contains("To=%2B15551212", body, StringComparison.Ordinal);
        Assert.Contains("From=%2B15550001", body, StringComparison.Ordinal);
        Assert.Contains("Body=hello+there", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Twilio_options_require_credentials()
    {
        var result = new SmsOptionsValidator().Validate(null, new SmsOptions { Provider = SmsOptions.Twilio });

        Assert.True(result.Failed);
        Assert.Contains("AccountSid", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Dry_run_is_the_default_provider()
    {
        Assert.Equal(SmsOptions.DryRun, new SmsOptions().Provider);
        Assert.False(new SmsOptionsValidator().Validate(null, new SmsOptions()).Failed);
    }

    private sealed class FixedOptions(SmsOptions value) : IOptionsMonitor<SmsOptions>
    {
        public SmsOptions CurrentValue { get; } = value;
        public SmsOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SmsOptions, string?> listener) => null;
    }
}
