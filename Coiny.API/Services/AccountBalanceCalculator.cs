using Coiny.API.Enums;

namespace Coiny.API.Services;

internal static class AccountBalanceCalculator
{
    public static decimal GetBalanceChange(AccountType accountType, CategoryType categoryType, decimal amount)
    {
        var assetChange = categoryType switch
        {
            CategoryType.Income => amount,
            CategoryType.Expense => -amount,
            CategoryType.Transfer => throw new InvalidOperationException("Transfers require paired account transactions and are not supported yet."),
            _ => throw new InvalidOperationException("Unsupported transaction category type.")
        };

        // Liability balances represent positive amounts owed.
        return accountType is AccountType.CreditCard or AccountType.Loan
            ? -assetChange
            : assetChange;
    }
}
