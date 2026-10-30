using System.Net;
using Microsoft.AspNetCore.Builder;
using Coiny.API.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coiny.API.Tests;

public class ReverseProxyTests
{
    [Theory]
    [InlineData("172.30.16.3", "https", "198.51.100.7")]
    [InlineData("172.30.16.8", "http", "172.30.16.8")]
    public async Task Only_the_configured_proxy_can_forward_scheme_and_client_address(string peer, string scheme, string client)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ReverseProxy:KnownProxy"] = "172.30.16.3"
        }).Build();
        var services = new ServiceCollection().AddLogging().AddTrustedReverseProxy(configuration).BuildServiceProvider();
        using (services) {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Scheme = "http";
            context.Request.Headers["X-Forwarded-For"] = "203.0.113.9, 198.51.100.7";
            context.Request.Headers["X-Forwarded-Proto"] = "http, https";
            var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
                services.GetRequiredService<ILoggerFactory>(), services.GetRequiredService<IOptions<ForwardedHeadersOptions>>());
            await middleware.Invoke(context);
            Assert.Equal(scheme, context.Request.Scheme);
            Assert.Equal(IPAddress.Parse(client), context.Connection.RemoteIpAddress);
        }
    }

    [Fact]
    public void Invalid_proxy_configuration_is_rejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ReverseProxy:KnownProxy"] = "trust-everyone"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTrustedReverseProxy(configuration));
    }
}
