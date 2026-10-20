using System.ServiceModel;

namespace WorkerKit.Soap;

/// <summary>
/// Generic WCF channel helpers. Generated service contracts stay in the connector.
/// </summary>
public static class SoapChannel
{
    /// <summary>
    /// Basic HTTP binding for an absolute endpoint. HTTPS uses transport security. HTTP uses none.
    /// </summary>
    /// <param name="endpoint">Absolute SOAP endpoint. The scheme selects the security mode.</param>
    /// <param name="timeout">Send, receive, open, and close timeout.</param>
    /// <returns>
    /// A <see cref="BasicHttpBinding"/> whose send, receive, open, and close timeouts equal <paramref name="timeout"/>.
    /// The maximum received message size and buffer size are 4 MiB.
    /// The reader quotas for string content and arrays are 1 MiB.
    /// </returns>
    /// <remarks>
    /// An <c>https</c> endpoint uses <see cref="BasicHttpSecurityMode.Transport"/>.
    /// Every other scheme, including HTTP, uses <see cref="BasicHttpSecurityMode.None"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="endpoint"/> is not an absolute URI.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is less than or equal to <see cref="TimeSpan.Zero"/>.</exception>
    public static BasicHttpBinding CreateBasicHttpBinding(Uri endpoint, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("SOAP endpoint must be an absolute URI.", nameof(endpoint));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "SOAP timeout must be positive.");
        }

        var security = endpoint.Scheme == Uri.UriSchemeHttps
            ? BasicHttpSecurityMode.Transport
            : BasicHttpSecurityMode.None;
        var binding = new BasicHttpBinding(security)
        {
            SendTimeout = timeout,
            ReceiveTimeout = timeout,
            OpenTimeout = timeout,
            CloseTimeout = timeout,
            MaxReceivedMessageSize = 4 * 1024 * 1024,
            MaxBufferSize = 4 * 1024 * 1024,
        };
        binding.ReaderQuotas.MaxStringContentLength = 1024 * 1024;
        binding.ReaderQuotas.MaxArrayLength = 1024 * 1024;
        return binding;
    }

    /// <summary>
    /// Aborts the channel when <paramref name="completed"/> is false.
    /// Call this from <c>finally</c> after a successful <c>CloseAsync</c> with <paramref name="completed"/> true.
    /// </summary>
    /// <param name="channel">Channel to abort when the operation did not complete.</param>
    /// <param name="completed"><see langword="false"/> aborts <paramref name="channel"/>. <see langword="true"/> does not.</param>
    /// <remarks>Aborts only when <paramref name="completed"/> is <see langword="false"/>. A completed channel is left as it is.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    public static void AbortUnlessCompleted(ICommunicationObject channel, bool completed)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!completed)
        {
            channel.Abort();
        }
    }
}
