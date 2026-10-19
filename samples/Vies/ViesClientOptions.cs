using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Samples.Vies;

/// <summary>
/// EU VIES <c>checkVat</c> endpoint. The checked-in WSDL still publishes an <c>http://</c> address.
/// WCF does not follow the redirect to HTTPS, so the sample calls the HTTPS URL directly.
/// </summary>
public sealed class ViesClientOptions
{
    public const string SectionName = "Vies";

    public const string DefaultEndpoint = "https://ec.europa.eu/taxation_customs/vies/services/checkVatService";

    [Required(ErrorMessage = "Vies:Endpoint is required.")]
    public string Endpoint { get; set; } = DefaultEndpoint;

    [Range(1, 300, ErrorMessage = "Vies:TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;
}
