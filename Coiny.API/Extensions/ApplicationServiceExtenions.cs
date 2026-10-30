using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Coiny.API.Data;

namespace Coiny.API.Extensions;

public static class ApplicationServceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, bool hybrid = false, bool scheduledJobs = true)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        if (!hybrid) services.AddScoped<DemoDataSeeder>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddDataProtection();
        services.AddScoped<Coiny.API.Services.Recovery.BackupEncryption>();
        if (hybrid) services.AddScoped<Coiny.API.Services.Recovery.HouseholdRecoveryService>();
        services.AddScoped<AccountSecurityService>();
        services.AddScoped<RecoveryCodesService>();
        services.AddSingleton<PasswordRecoveryEmail>();
        services.AddSingleton<IPasswordRecoveryEmail>(services => services.GetRequiredService<PasswordRecoveryEmail>());
        services.AddHostedService(services => services.GetRequiredService<PasswordRecoveryEmail>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IHouseholdInvitationService, HouseholdInvitationService>();
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IInstitutionService, InstitutionService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<ITransactionImportService, TransactionImportService>();
        services.AddScoped<ImportSessionService>();
            services.AddScoped<ImportUndoService>();
        services.AddScoped<AccountReconciliationService>();
        services.AddScoped<IAccountBalanceService, AccountBalanceService>();
        services.AddScoped<IAccountBalanceSnapshotService, AccountBalanceSnapshotService>();
        services.AddScoped<IAccountTrendService, AccountTrendService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITrendService, TrendService>();
        services.AddSingleton<IBudgetScheduleService, BudgetScheduleService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<BillPaymentService>();
        services.AddScoped<SavingsFundsService>();
        services.AddScoped<BudgetActualsService>();
        services.AddScoped<SavedCsvMappingsService>();
        services.AddScoped<MerchantRulesService>();
        services.AddScoped<RecurringExpenseService>();
        services.AddScoped<RecurringAutomationService>();
        services.AddScoped<HouseholdAlertsService>();
        if (scheduledJobs) services.AddHostedService<RecurringScanWorker>();
        services.AddScoped<TransactionManagementService>();
        services.AddScoped<UserPreferencesService>();


        return services;
    }
}
