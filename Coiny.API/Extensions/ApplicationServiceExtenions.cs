using Coiny.API.Interfaces;
using Coiny.API.Services;

namespace Coiny.API.Extensions;

public static class ApplicationServceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}