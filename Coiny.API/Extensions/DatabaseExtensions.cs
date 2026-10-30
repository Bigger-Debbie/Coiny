using Coiny.API.Data;
using Coiny.API.Data.Hybrid;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("HybridStorage:Enabled")) {
            services.AddHybridStorageFoundation(configuration);
            services.AddScoped<IControlContext>(provider => provider.GetRequiredService<ControlContext>());
            services.AddScoped<HouseholdRequestContext>();
            services.AddScoped<HybridPreferenceChanges>();
            services.AddScoped<IFinancialContext>(provider => provider.GetRequiredService<HouseholdRequestContext>().Context
                ?? throw new InvalidOperationException("No authorized household scope was opened for this operation."));
            return services;
        }
        services.AddDbContext<CoinyContext>(options => 
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IControlContext>(provider => provider.GetRequiredService<CoinyContext>());
        services.AddScoped<IFinancialContext>(provider => provider.GetRequiredService<CoinyContext>());
        return services; 
    }
}