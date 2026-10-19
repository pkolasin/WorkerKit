using WorkerKit.Abstractions.Queues;
using WorkerKit.Hosting.Queues;

namespace Contoso.Billing;

public sealed record SampleQueuePayload(string Message);

public sealed class SampleQueueHandler(ILogger<SampleQueueHandler> logger) : IWorkItemHandler<SampleQueuePayload>
{
    public Task HandleAsync(WorkItem<SampleQueuePayload> item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Sample queue item {Id}: {Message}", item.Id, item.Payload.Message);
        return Task.CompletedTask;
    }
}

public sealed class SampleQueueWorker : QueueWorker<SampleQueuePayload>
{
    public const string WorkerNameValue = "sample-queue";

    public SampleQueueWorker(
        WorkerContext context,
        InMemoryWorkQueue<SampleQueuePayload> queue,
        IOptionsMonitor<QueueWorkerOptions> options)
        : base(context, queue, options)
    {
    }

    protected override string WorkerName => WorkerNameValue;
}

public static partial class ContosoBillingConnectorExtensions
{
    static partial void AddSampleQueue(IHostApplicationBuilder builder)
    {
        builder.Services.AddInMemoryQueueWorker<SampleQueueWorker, SampleQueuePayload, SampleQueueHandler>(
            builder.Configuration,
            SampleQueueWorker.WorkerNameValue);
    }
}
