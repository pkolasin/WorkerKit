namespace WorkerKit.Samples.Vies.Queue;

/// <summary>
/// Extra settings bound beside <c>QueueWorkerOptions</c> on <c>Queue:vies-queue</c>.
/// Both country code and VAT number must be set before the worker enqueues a sample item.
/// </summary>
public sealed class ViesQueueOptions
{
    public const string SectionName = "Queue:vies-queue";

    public string CountryCode { get; set; } = "";

    public string VatNumber { get; set; } = "";
}
