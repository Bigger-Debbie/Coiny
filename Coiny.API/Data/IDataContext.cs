using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Coiny.API.Data;

public interface IDataContext
{
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    DbSet<T> Set<T>() where T : class;
    EntityEntry<T> Add<T>(T entity) where T : class;
    EntityEntry<T> Remove<T>(T entity) where T : class;
    void RemoveRange(IEnumerable<object> entities);
    void RemoveRange(params object[] entities);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IControlContext : IDataContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<AuthRecoveryCode> AuthRecoveryCodes { get; }
    DbSet<AuthSession> AuthSessions { get; }
    DbSet<HouseholdInvitation> HouseholdInvitations { get; }
    DbSet<Household> Households { get; }
    DbSet<HouseholdMember> HouseholdMembers { get; }
}

public interface IFinancialContext : IDataContext
{
    DbSet<SavedCsvMapping> SavedCsvMappings { get; }
    DbSet<MerchantCategoryRule> MerchantCategoryRules { get; }
    DbSet<HouseholdRecurringSetting> HouseholdRecurringSettings { get; }
    DbSet<HouseholdAlert> HouseholdAlerts { get; }
    DbSet<HouseholdAlertRead> HouseholdAlertReads { get; }
    DbSet<RecurringExpenseReview> RecurringExpenseReviews { get; }
    DbSet<Institution> Institutions { get; }
    DbSet<AccountReconciliation> AccountReconciliations { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Category> Categories { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<TransactionImportSession> TransactionImportSessions { get; }
    DbSet<TransactionImportBatch> TransactionImportBatches { get; }
    DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots { get; }
    DbSet<HouseholdBudget> HouseholdBudgets { get; }
    DbSet<BudgetRevision> BudgetRevisions { get; }
    DbSet<BudgetIncomeSource> BudgetIncomeSources { get; }
    DbSet<BillPayment> BillPayments { get; }
    DbSet<BudgetCategoryRule> BudgetCategoryRules { get; }
    DbSet<TransactionBudgetAssignment> TransactionBudgetAssignments { get; }
    DbSet<BudgetAllocation> BudgetAllocations { get; }
}
