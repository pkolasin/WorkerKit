using System.ServiceModel;
using WorkerKit.Soap;

namespace WorkerKit.Hosting.Tests;

public sealed class SoapChannelTests
{
    [Fact]
    public void Https_endpoint_uses_transport_security_and_the_timeout()
    {
        var binding = SoapChannel.CreateBasicHttpBinding(new Uri("https://example.test/soap"), TimeSpan.FromSeconds(3));

        Assert.Equal(BasicHttpSecurityMode.Transport, binding.Security.Mode);
        Assert.Equal(TimeSpan.FromSeconds(3), binding.SendTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), binding.ReceiveTimeout);
    }

    [Fact]
    public void Http_endpoint_uses_no_transport_security()
    {
        var binding = SoapChannel.CreateBasicHttpBinding(new Uri("http://example.test/soap"), TimeSpan.FromSeconds(1));

        Assert.Equal(BasicHttpSecurityMode.None, binding.Security.Mode);
    }

    [Fact]
    public void Relative_endpoint_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SoapChannel.CreateBasicHttpBinding(new Uri("/soap", UriKind.Relative), TimeSpan.FromSeconds(1)));
    }
}
