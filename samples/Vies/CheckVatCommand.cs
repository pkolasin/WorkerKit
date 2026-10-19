using WorkerKit.Abstractions.Commands;
using WorkerKit.Hosting.Cli;

namespace WorkerKit.Samples.Vies;

/// <summary>Looks up one EU VAT number through VIES. Exit 0 when the service says the number is valid.</summary>
public sealed class CheckVatCommand(IViesVatChecker vies) : IWorkerCommand
{
    public static readonly CommandOption<string> CountryCode = new(
        "--country-code",
        "ISO 3166-1 alpha-2 country code, for example DE.")
    {
        Required = true,
    };

    public static readonly CommandOption<string> VatNumber = new(
        "--vat-number",
        "VAT number without the country prefix. Spaces are removed. A repeated country prefix is stripped.")
    {
        Required = true,
    };

    public static CommandSpec Spec { get; } = new("check-vat", "Check one EU VAT number through the VIES SOAP service.")
    {
        CountryCode,
        VatNumber,
    };

    public static bool EnableTelemetry => true;

    public async Task<int> ExecuteAsync(ICommandArgs args, CancellationToken cancellationToken)
    {
        var result = await vies.CheckAsync(
            args.Get(CountryCode) ?? "",
            args.Get(VatNumber) ?? "",
            cancellationToken).ConfigureAwait(false);
        return result.Valid ? ExitCodes.Success : ExitCodes.Error;
    }
}
