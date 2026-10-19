namespace WorkerKit.Samples.Outbound.Queue;

/// <summary>
/// Extra settings on <c>Queue:outbound-queue</c>. All three example fields must be set to enqueue one item at start.
/// </summary>
public sealed class OutboundQueueOptions
{
    public const string SectionName = "Queue:outbound-queue";

    public string ExampleKind { get; set; } = "";

    public string ExampleDestination { get; set; } = "";

    public string ExampleBody { get; set; } = "";
}
