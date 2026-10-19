using System.Diagnostics;
using System.ServiceModel;
using WorkerKit.Samples.Vies.Generated;
using WorkerKit.Soap;

namespace WorkerKit.Samples.Vies;

/// <summary>
/// Calls VIES <c>checkVat</c>. A new <see cref="checkVatPortTypeClient"/> is created per call and closed or aborted.
/// Do not cache <c>ClientBase&lt;T&gt;</c>: a faulted channel cannot be reused. This type itself is a singleton.
/// </summary>
public sealed class ViesVatChecker : IViesVatChecker
{
    private readonly IOptionsMonitor<ViesClientOptions> _options;
    private readonly ILogger<ViesVatChecker> _logger;

    public ViesVatChecker(IOptionsMonitor<ViesClientOptions> options, ILogger<ViesVatChecker> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<ViesVatResult> CheckAsync(string countryCode, string vatNumber, CancellationToken cancellationToken)
    {
        var country = ViesVatNumber.NormalizeCountry(countryCode);
        var number = ViesVatNumber.NormalizeNumber(country, vatNumber);
        using var activity = ViesTelemetry.Source.StartActivity(ViesTelemetry.CheckVatSpanName, ActivityKind.Client);
        activity?.SetTag("vat.country", country);
        activity?.SetTag("vat.number", number);
        var started = Stopwatch.GetTimestamp();
        var options = _options.CurrentValue;
        var client = CreateClient(options.Endpoint, TimeSpan.FromSeconds(options.TimeoutSeconds));
        var closed = false;
        try
        {
            _logger.LogInformation("Requesting VIES checkVat for {Country}{VatNumber}", country, number);
            var response = await client
                .checkVatAsync(new checkVatRequest(country, number))
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            await client.CloseAsync().ConfigureAwait(false);
            closed = true;
            var result = new ViesVatResult(
                response.countryCode ?? country,
                response.vatNumber ?? number,
                response.valid,
                response.name,
                response.address,
                response.requestDate);
            activity?.SetTag("vat.valid", result.Valid);
            activity?.SetStatus(ActivityStatusCode.Ok);
            ViesTelemetry.RecordCall(Stopwatch.GetElapsedTime(started), "success");
            _logger.LogInformation(
                "VIES checkVat for {Country}{VatNumber} is {Valid}",
                result.CountryCode,
                result.VatNumber,
                result.Valid);
            return result;
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            ViesTelemetry.RecordCall(Stopwatch.GetElapsedTime(started), "failure");
            throw;
        }
        finally
        {
            SoapChannel.AbortUnlessCompleted(client, closed);
        }
    }

    private static checkVatPortTypeClient CreateClient(string endpoint, TimeSpan timeout)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Vies:Endpoint '{endpoint}' is not an absolute URI.");
        }

        return new checkVatPortTypeClient(SoapChannel.CreateBasicHttpBinding(uri, timeout), new EndpointAddress(uri));
    }
}
