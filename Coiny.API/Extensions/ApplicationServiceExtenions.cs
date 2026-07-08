using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;

namespace Coiny.API.Extensions;

public static class ApplicationServceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IInstitutionService, InstitutionService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IAccountBalanceService, AccountBalanceService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}