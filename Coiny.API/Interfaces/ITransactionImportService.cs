using Coiny.API.DTOs.Transactions;

namespace Coiny.API.Interfaces;

public interface ITransactionImportService
{
    Task<TransactionImportValidation> ValidateAsync(TransactionImportRequest request);
    Task<TransactionImportResponse> ImportAsync(TransactionImportRequest request);
    Task<IReadOnlyList<TransactionImportResponse>> HistoryAsync(int? accountId);
}
