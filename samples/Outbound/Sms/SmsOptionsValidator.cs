namespace WorkerKit.Samples.Outbound.Sms;

public sealed class SmsOptionsValidator : IValidateOptions<SmsOptions>
{
    public ValidateOptionsResult Validate(string? name, SmsOptions options)
    {
        if (string.Equals(options.Provider, SmsOptions.DryRun, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Success;
        }

        if (!string.Equals(options.Provider, SmsOptions.Twilio, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("Sms:Provider must be DryRun or Twilio.");
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.AccountSid))
        {
            errors.Add("Sms:AccountSid is required when Sms:Provider is Twilio.");
        }

        if (string.IsNullOrWhiteSpace(options.AuthToken))
        {
            errors.Add("Sms:AuthToken is required when Sms:Provider is Twilio.");
        }

        if (string.IsNullOrWhiteSpace(options.From))
        {
            errors.Add("Sms:From is required when Sms:Provider is Twilio.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
