using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkerKit.Abstractions.Options;
using WorkerKit.Hosting.Http;

namespace WorkerKit.Hosting.Tests;

public sealed class StreamingClientTests
{
    [Fact]
    public void Streaming_client_does_not_apply_a_request_timeout()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Download:BaseAddress"] = "https://example.invalid/files",
            ["Download:UserAgent"] = "register-ingestion/1",
            ["Download:Resilience:AttemptTimeoutSeconds"] = "7",
        }).Build();
        var services = new ServiceCollection();
        services.AddStreamingClient<IDownloadClient, DownloadClient, DownloadOptions>(configuration.GetSection("Download"));
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IDownloadClient>().Http;

        Assert.Equal(new Uri("https://example.invalid/files/"), client.BaseAddress);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
        Assert.Equal("register-ingestion/1", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Fact]
    public async Task SendStreaming_returns_before_the_body_is_read()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new GatedHandler(gate.Task));
        var pending = client.SendStreamingAsync(new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/file.bin"));

        var winner = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(pending, winner);

        gate.SetResult();
        using var response = await pending;
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 7 }, body);
    }

    [Fact]
    public async Task SendStreaming_rejects_a_null_client_or_request()
    {
        using var client = new HttpClient();
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.SendStreamingAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => HttpClientRegistration.SendStreamingAsync(null!, new HttpRequestMessage()));
    }

    private sealed class DownloadOptions : HttpClientEndpointOptions;

    private interface IDownloadClient
    {
        HttpClient Http { get; }
    }

    private sealed class DownloadClient(HttpClient http) : IDownloadClient
    {
        public HttpClient Http { get; } = http;
    }

    private sealed class GatedHandler(Task gate) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new GatedStream(gate)),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class GatedStream(Task gate) : Stream
    {
        private bool _written;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_written || buffer.Length == 0)
            {
                return 0;
            }

            buffer.Span[0] = 7;
            _written = true;
            return 1;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
