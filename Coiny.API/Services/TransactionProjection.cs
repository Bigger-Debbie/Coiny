using System.Linq.Expressions;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Models;

namespace Coiny.API.Services;

internal static class TransactionProjection
{
    public static readonly Expression<Func<Transaction, TransactionResponse>> Response = t => new TransactionResponse
    {
        EditVersion = t.EditVersion, Notes = t.Notes, ImportBatchId = t.ImportBatchId,
        Id = t.Id, AccountId = t.AccountId, CategoryId = t.CategoryId,
        AccountName = t.Account.Name,
        AccountType = t.Account.AccountType,
        InstitutionName = t.Account.Institution != null ? t.Account.Institution.Name : null,
        CategoryName = t.Category.Name, CategoryType = t.Category.CategoryType,
        Amount = t.Amount, TransactionDate = t.TransactionDate, Description = t.Description,
        Merchant = t.Merchant, IsCleared = t.IsCleared,
        TransferDirection = t.TransferDirection, TransferCounterparty = t.TransferCounterparty,
        PairedTransactionId = t.PairedTransactionId,
        CounterpartyAccountId = t.PairedTransaction != null ? t.PairedTransaction.AccountId : null,
        CounterpartyAccountName = t.PairedTransaction != null ? t.PairedTransaction.Account.Name : null,
        CounterpartyAccountType = t.PairedTransaction != null ? t.PairedTransaction.Account.AccountType : null,
    };
}
