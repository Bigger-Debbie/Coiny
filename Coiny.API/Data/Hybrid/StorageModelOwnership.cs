using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data.Hybrid;

// Explicit inventory: new mapped types must be assigned deliberately (see boundary tests).
public static class StorageModelOwnership
{
    public static IReadOnlyDictionary<Type, string> Control { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<Type, string>(new Dictionary<Type, string>
        {
            [typeof(ApplicationUser)] = "AspNetUsers",
            [typeof(AuthRecoveryCode)] = "AuthRecoveryCodes",
            [typeof(AuthSession)] = "AuthSessions",
            [typeof(Household)] = "Households",
            [typeof(HouseholdMember)] = "HouseholdMembers",
            [typeof(HouseholdInvitation)] = "HouseholdInvitations",
        });

    public static IReadOnlyDictionary<Type, string> Financial { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<Type, string>(new Dictionary<Type, string>
        {
            [typeof(Account)] = "Accounts",
            [typeof(AccountBalanceSnapshot)] = "AccountBalanceSnapshots",
            [typeof(AccountReconciliation)] = "AccountReconciliations",
            [typeof(BillPayment)] = "BillPayments",
            [typeof(BillPaymentTransaction)] = "BillPaymentTransaction",
            [typeof(BudgetAllocation)] = "BudgetAllocations",
            [typeof(BudgetCategoryRule)] = "BudgetCategoryRules",
            [typeof(BudgetIncomeSource)] = "BudgetIncomeSources",
            [typeof(BudgetRevision)] = "BudgetRevisions",
            [typeof(Category)] = "Categories",
            [typeof(HouseholdAlert)] = "HouseholdAlerts",
            [typeof(HouseholdAlertRead)] = "HouseholdAlertReads",
            [typeof(HouseholdBudget)] = "HouseholdBudgets",
            [typeof(HouseholdRecurringSetting)] = "HouseholdRecurringSettings",
            [typeof(Institution)] = "Institutions",
            [typeof(MerchantCategoryRule)] = "MerchantCategoryRules",
            [typeof(RecurringExpenseReview)] = "RecurringExpenseReviews",
            [typeof(SavedCsvMapping)] = "SavedCsvMappings",
            [typeof(SavingsActivity)] = "SavingsActivity",
            [typeof(SavingsFund)] = "SavingsFund",
            [typeof(Transaction)] = "Transactions",
            [typeof(TransactionBudgetAssignment)] = "TransactionBudgetAssignments",
            [typeof(TransactionImportBatch)] = "TransactionImportBatches",
            [typeof(TransactionImportSession)] = "TransactionImportSessions",
            [typeof(TransactionImportUndo)] = "TransactionImportUndo",
        });

    internal static void NameTables(ModelBuilder builder, IReadOnlyDictionary<Type, string> entities)
    {
        foreach (var (type, table) in entities) builder.Entity(type).ToTable(table);
    }
}
