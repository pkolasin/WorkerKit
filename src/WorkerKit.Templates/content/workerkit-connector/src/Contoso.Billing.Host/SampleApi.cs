namespace Contoso.Billing;

public sealed class SampleWorkerOptions
{
    public const string SectionName = "Sample";

    /// <summary>When false, <c>run</c> does not call the sample API.</summary>
    public bool Enabled { get; set; }

    [Range(1, 86_400, ErrorMessage = "Sample:PollIntervalSeconds must be between 1 and 86400.")]
    public int PollIntervalSeconds { get; set; } = 300;

    /// <summary>Empty skips the HTTP call. The sample endpoint stays offline until this is set and <see cref="Enabled"/> is true.</summary>
    public string ItemId { get; set; } = "";
}

public sealed class SampleApiOptions : HttpClientEndpointOptions
{
    public const string SectionName = "SampleApi";

    public SampleApiOptions()
    {
        BaseAddress = "https://example.invalid/api/";
        UserAgent = "Contoso.Billing/1.0";
    }
}

public interface ISampleApiClient
{
    Task<string> GetAsync(string id, CancellationToken cancellationToken);
}

public sealed class SampleApiClient(HttpClient httpClient) : ISampleApiClient
{
    public async Task<string> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        using var response = await httpClient.GetAsync("items/" + Uri.EscapeDataString(id), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
