using WorkerKit.Abstractions.Queues;
using WorkerKit.Samples.Vies;

namespace WorkerKit.Samples.Vies.Queue;

/// <summary>Runs one VIES lookup. A transport failure is retried. An invalid number is a completed item.</summary>
public sealed class ViesQueueHandler(IViesVatChecker vies, ILogger<ViesQueueHandler> logger) : IWorkItemHandler<ViesQueuePayload>
{
    public async Task HandleAsync(WorkItem<ViesQueuePayload> item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var result = await vies.CheckAsync(item.Payload.CountryCode, item.Payload.VatNumber, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Handled VIES queue item {Id} attempt {Attempt}: {Country}{VatNumber} valid={Valid}",
            item.Id,
            item.Attempt,
            result.CountryCode,
            result.VatNumber,
            result.Valid);
    }
}
