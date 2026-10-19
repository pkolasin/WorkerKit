using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Samples.Outbound.Sms;

/// <summary>
/// SMS provider. <see cref="DryRun"/> logs the message and is the default.
/// <see cref="Twilio"/> posts to the Twilio Messages API.
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public const string DryRun = "DryRun";

    public const string Twilio = "Twilio";

    public string Provider { get; set; } = DryRun;

    public string AccountSid { get; set; } = "";

    public string AuthToken { get; set; } = "";

    public string From { get; set; } = "";

    [Range(1, 120, ErrorMessage = "Sms:TimeoutSeconds must be between 1 and 120.")]
    public int TimeoutSeconds { get; set; } = 30;
}
