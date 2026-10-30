using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Coiny.API.Extensions;

public static class ReverseProxyExtensions
{
    public static IServiceCollection AddTrustedReverseProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var address = configuration["ReverseProxy:KnownProxy"];
        if (string.IsNullOrWhiteSpace(address)) return services;
        if (!IPAddress.TryParse(address, out var proxy))
            throw new InvalidOperationException("ReverseProxy:KnownProxy must be an IP address.");
        services.Configure<ForwardedHeadersOptions>(options => {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownProxies.Add(proxy);
        });
        return services;
    }
}
