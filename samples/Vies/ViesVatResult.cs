namespace WorkerKit.Samples.Vies;

/// <summary>One <c>checkVat</c> response. <see cref="Valid"/> is the service answer, not a transport success.</summary>
public sealed record ViesVatResult(
    string CountryCode,
    string VatNumber,
    bool Valid,
    string? Name,
    string? Address,
    DateTime RequestDate);
