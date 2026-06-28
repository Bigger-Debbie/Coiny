using Coiny.API.Data;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;

namespace Coiny.API.Extensions;

public static class IdentityExtesnsions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services
            .AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<CoinyContext>()
            .AddDefaultTokenProviders();

        return services;
    }
}