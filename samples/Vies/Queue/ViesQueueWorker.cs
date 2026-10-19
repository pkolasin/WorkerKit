using WorkerKit.Abstractions.Options;
using WorkerKit.Hosting.Queues;
using WorkerKit.Hosting.Workers;

namespace WorkerKit.Samples.Vies.Queue;

/// <summary>
/// In-memory queue of VIES lookups. <c>Queue:vies-queue:Enabled</c> defaults to false,
/// so <c>run</c> does not poll this queue until it is turned on.
/// </summary>
public sealed class ViesQueueWorker : QueueWorker<ViesQueuePayload>
{
    public const string WorkerNameValue = "vies-queue";

    private readonly InMemoryWorkQueue<ViesQueuePayload> _queue;
    private readonly IOptionsMonitor<ViesQueueOptions> _vies;

    public ViesQueueWorker(
        WorkerContext context,
        InMemoryWorkQueue<ViesQueuePayload> queue,
        IOptionsMonitor<QueueWorkerOptions> options,
        IOptionsMonitor<ViesQueueOptions> vies)
        : base(context, queue, options)
    {
        ArgumentNullException.ThrowIfNull(vies);
        _queue = queue;
        _vies = vies;
    }

    protected override string WorkerName => WorkerNameValue;

    protected override void LogDisabled()
    {
        Logger.LogInformation("VIES queue worker is disabled (Queue:vies-queue:Enabled = false)");
    }

    protected override Task OnStartingAsync(CancellationToken cancellationToken)
    {
        var options = _vies.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.CountryCode) || string.IsNullOrWhiteSpace(options.VatNumber))
        {
            Logger.LogInformation(
                "VIES queue worker started. Set Queue:vies-queue:CountryCode and VatNumber to enqueue one lookup");
            return Task.CompletedTask;
        }

        var country = options.CountryCode.Trim();
        var number = options.VatNumber.Trim();
        _queue.Enqueue(new ViesQueuePayload(country, number), country + number);
        Logger.LogInformation("Enqueued VIES lookup {Country}{VatNumber}", country, number);
        return Task.CompletedTask;
    }
}
