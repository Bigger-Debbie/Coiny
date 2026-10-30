using Coiny.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data;

public class CoinyContext : IdentityDbContext<ApplicationUser>, IControlContext, IFinancialContext
{
    public CoinyContext(DbContextOptions<CoinyContext> options) : base(options) { }

    public DbSet<AuthRecoveryCode> AuthRecoveryCodes => Set<AuthRecoveryCode>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    public DbSet<SavedCsvMapping> SavedCsvMappings => Set<SavedCsvMapping>();
    public DbSet<MerchantCategoryRule> MerchantCategoryRules => Set<MerchantCategoryRule>();
    public DbSet<HouseholdRecurringSetting> HouseholdRecurringSettings => Set<HouseholdRecurringSetting>();
    public DbSet<HouseholdAlert> HouseholdAlerts => Set<HouseholdAlert>();
    public DbSet<HouseholdAlertRead> HouseholdAlertReads => Set<HouseholdAlertRead>();
    public DbSet<RecurringExpenseReview> RecurringExpenseReviews => Set<RecurringExpenseReview>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<AccountReconciliation> AccountReconciliations => Set<AccountReconciliation>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionImportSession> TransactionImportSessions => Set<TransactionImportSession>();
    public DbSet<TransactionImportBatch> TransactionImportBatches => Set<TransactionImportBatch>();
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();
    public DbSet<HouseholdBudget> HouseholdBudgets => Set<HouseholdBudget>();
    public DbSet<BudgetRevision> BudgetRevisions => Set<BudgetRevision>();
    public DbSet<BudgetIncomeSource> BudgetIncomeSources => Set<BudgetIncomeSource>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<BudgetCategoryRule> BudgetCategoryRules => Set<BudgetCategoryRule>();
    public DbSet<TransactionBudgetAssignment> TransactionBudgetAssignments => Set<TransactionBudgetAssignment>();
    public DbSet<BudgetAllocation> BudgetAllocations => Set<BudgetAllocation>();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ReconciliationInvalidation.StageAsync(this, cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(CoinyContext).Assembly);
    }
}
