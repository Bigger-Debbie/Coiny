using Coiny.API.Data.Configurations;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data.Hybrid;

// Financial model opened through an authorized registered household storage scope.
public sealed class HouseholdContext(DbContextOptions<HouseholdContext> options) : DbContext(options), IFinancialContext
{
    public DbSet<SavedCsvMapping> SavedCsvMappings => Set<SavedCsvMapping>();
    public DbSet<MerchantCategoryRule> MerchantCategoryRules => Set<MerchantCategoryRule>();
    public DbSet<HouseholdRecurringSetting> HouseholdRecurringSettings => Set<HouseholdRecurringSetting>();
    public DbSet<HouseholdAlert> HouseholdAlerts => Set<HouseholdAlert>();
    public DbSet<HouseholdAlertRead> HouseholdAlertReads => Set<HouseholdAlertRead>();
    public DbSet<RecurringExpenseReview> RecurringExpenseReviews => Set<RecurringExpenseReview>();
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfiguration(new AccountBalanceSnapshotConfiguration());
        builder.ApplyConfiguration(new AccountLoanConfiguration());
        builder.ApplyConfiguration(new AccountReconciliationConfiguration());
        builder.ApplyConfiguration(new BillPaymentConfiguration());
        builder.ApplyConfiguration(new BillPaymentTransactionConfiguration());
        builder.ApplyConfiguration(new BudgetCategoryRuleConfiguration());
        builder.ApplyConfiguration(new TransactionBudgetAssignmentConfiguration());
        builder.ApplyConfiguration(new BudgetAllocationConfiguration());
        builder.ApplyConfiguration(new BudgetIncomeSourceConfiguration());
        builder.ApplyConfiguration(new BudgetRevisionConfiguration());
        builder.ApplyConfiguration(new CategoryConfiguration());
        builder.ApplyConfiguration(new HouseholdBudgetConfiguration());
        builder.ApplyConfiguration(new MerchantCategoryRuleConfiguration());
        builder.ApplyConfiguration(new HouseholdRecurringSettingConfiguration());
        builder.ApplyConfiguration(new HouseholdAlertConfiguration());
        builder.ApplyConfiguration(new HouseholdAlertReadConfiguration());
        builder.ApplyConfiguration(new RecurringExpenseReviewConfiguration());
        builder.ApplyConfiguration(new SavedCsvMappingConfiguration());
        builder.ApplyConfiguration<SavingsFund>(new SavingsFundConfiguration());
        builder.ApplyConfiguration<SavingsActivity>(new SavingsFundConfiguration());
        builder.ApplyConfiguration(new TransactionConfiguration());
        builder.ApplyConfiguration(new TransactionImportBatchConfiguration());
        builder.ApplyConfiguration(new TransactionImportSessionConfiguration());
        builder.ApplyConfiguration(new TransactionImportUndoConfiguration());
        // Existing domain navigations remain for the legacy context. They are not
        // mapped across stores in this model; stable scalar IDs remain available.
        foreach (var type in StorageModelOwnership.Control.Keys) builder.Ignore(type);
        StorageModelOwnership.NameTables(builder, StorageModelOwnership.Financial);
        builder.Entity<PreferenceChangeReceipt>().ToTable("PreferenceChangeReceipts").HasKey(x => x.Id);
        builder.Entity<HouseholdDatabaseMetadata>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.ToTable("HouseholdDatabaseMetadata", table => table.HasCheckConstraint("CK_HouseholdDatabaseMetadata_Singleton", "Id = 1"));
        });
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ReconciliationInvalidation.StageAsync(this, cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
