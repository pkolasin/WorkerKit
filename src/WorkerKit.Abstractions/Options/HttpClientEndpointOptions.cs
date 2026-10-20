using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

/// <summary>Base address, user agent, and resilience settings for an HTTP client.</summary>
public class HttpClientEndpointOptions
{
    /// <summary>Base address of the client.</summary>
    /// <remarks>The initializer is <c>""</c>. <see cref="RequiredAttribute"/> marks the value required.</remarks>
    [Required(ErrorMessage = "BaseAddress is required.")]
    public string BaseAddress { get; set; } = "";

    /// <summary>User-Agent value for the client.</summary>
    /// <remarks>The initializer is <c>WorkerKit/0.1</c>.</remarks>
    public string UserAgent { get; set; } = "WorkerKit/0.1";

    /// <summary>Resilience settings for the client.</summary>
    /// <remarks>The initializer is a new <see cref="HttpResilienceOptions"/>.</remarks>
    public HttpResilienceOptions Resilience { get; set; } = new();
}
