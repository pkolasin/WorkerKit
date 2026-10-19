namespace WorkerKit.Samples.Vies;

public interface IViesVatChecker
{
    Task<ViesVatResult> CheckAsync(string countryCode, string vatNumber, CancellationToken cancellationToken);
}
