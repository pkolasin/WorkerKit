using System.ComponentModel.DataAnnotations;

namespace WorkerKit.Abstractions.Options;

public class HttpClientEndpointOptions
{
    [Required(ErrorMessage = "BaseAddress is required.")]
    public string BaseAddress { get; set; } = "";

    public string UserAgent { get; set; } = "WorkerKit/0.1";

    public HttpResilienceOptions Resilience { get; set; } = new();
}
