namespace Contoso.Billing;

/// <summary>
/// Sample periodic worker. <see cref="IsEnabled"/> follows <c>Sample:Enabled</c>, which defaults to false
/// so <c>run</c> does not call the placeholder endpoint.
/// </summary>
public sealed class SamplePollingWorker : PeriodicWorker
{
    public const string WorkerNameValue = "sample-poll";

    private readonly IOptionsMonitor<SampleWorkerOptions> _options;

    public SamplePollingWorker(WorkerContext context, IOptionsMonitor<SampleWorkerOptions> options)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    protected override string WorkerName => WorkerNameValue;

    protected override TimeSpan Interval =>
        TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.PollIntervalSeconds));

    protected override bool IsEnabled => _options.CurrentValue.Enabled;

    protected override void LogDisabled()
    {
        Logger.LogInformation("Sample worker is disabled (Sample:Enabled = false). The host does not call the sample API.");
    }

    protected override async Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var id = _options.CurrentValue.ItemId;
        if (string.IsNullOrWhiteSpace(id))
        {
            Logger.LogInformation("Sample heartbeat. Set Sample:ItemId to call the sample API.");
            return 0;
        }

        var client = services.GetRequiredService<ISampleApiClient>();
        var body = await client.GetAsync(id.Trim(), cancellationToken).ConfigureAwait(false);
        Logger.LogInformation("Sample fetched {Id} ({Length} characters)", id.Trim(), body.Length);
        return 1;
    }
}
