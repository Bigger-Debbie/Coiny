using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;

namespace Coiny.API.Extensions;

public static class IdentityExtesnsions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, bool hybrid = false)
    {
        var identity = services
            .AddIdentity<ApplicationUser, IdentityRole>(options => {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            });
        if (hybrid) identity.AddEntityFrameworkStores<ControlContext>();
        else identity.AddEntityFrameworkStores<CoinyContext>();
        identity.AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));
        return services;
    }
}