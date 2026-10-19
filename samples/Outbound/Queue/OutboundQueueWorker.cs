using WorkerKit.Abstractions.Options;
using WorkerKit.Hosting.Queues;
using WorkerKit.Hosting.Workers;

namespace WorkerKit.Samples.Outbound.Queue;

/// <summary>
/// In-memory outbound queue. <c>Queue:outbound-queue:Enabled</c> defaults to false.
/// </summary>
public sealed class OutboundQueueWorker : QueueWorker<OutboundMessage>
{
    public const string WorkerNameValue = "outbound-queue";

    private readonly InMemoryWorkQueue<OutboundMessage> _queue;
    private readonly IOptionsMonitor<OutboundQueueOptions> _outbound;

    public OutboundQueueWorker(
        WorkerContext context,
        InMemoryWorkQueue<OutboundMessage> queue,
        IOptionsMonitor<QueueWorkerOptions> options,
        IOptionsMonitor<OutboundQueueOptions> outbound)
        : base(context, queue, options)
    {
        ArgumentNullException.ThrowIfNull(outbound);
        _queue = queue;
        _outbound = outbound;
    }

    protected override string WorkerName => WorkerNameValue;

    protected override void LogDisabled()
    {
        Logger.LogInformation("Outbound queue worker is disabled (Queue:outbound-queue:Enabled = false)");
    }

    protected override Task OnStartingAsync(CancellationToken cancellationToken)
    {
        var options = _outbound.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ExampleKind)
            || string.IsNullOrWhiteSpace(options.ExampleDestination)
            || string.IsNullOrWhiteSpace(options.ExampleBody))
        {
            Logger.LogInformation(
                "Outbound queue worker started. Set Queue:outbound-queue:ExampleKind, ExampleDestination, and ExampleBody to enqueue one item");
            return Task.CompletedTask;
        }

        var message = new OutboundMessage(options.ExampleKind.Trim(), options.ExampleDestination.Trim(), options.ExampleBody);
        _queue.Enqueue(message, message.Kind + ":" + message.Destination);
        Logger.LogInformation("Enqueued outbound {Kind} to {Destination}", message.Kind, message.Destination);
        return Task.CompletedTask;
    }
}
