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
    public static void AbortUnlessCompleted(ICommunicationObject channel, bool completed)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!completed)
        {
            channel.Abort();
        }
    }
}
