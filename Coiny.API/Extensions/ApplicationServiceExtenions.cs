using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Coiny.API.Data;

namespace Coiny.API.Extensions;

public static class ApplicationServceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<DemoDataSeeder>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IHouseholdInvitationService, HouseholdInvitationService>();
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IInstitutionService, InstitutionService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<IAccountBalanceService, AccountBalanceService>();
        services.AddScoped<IAccountBalanceSnapshotService, AccountBalanceSnapshotService>();
        services.AddScoped<IAccountTrendService, AccountTrendService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITrendService, TrendService>();
        services.AddSingleton<IBudgetScheduleService, BudgetScheduleService>();
        services.AddScoped<IBudgetService, BudgetService>();

        return services;
    }
}
