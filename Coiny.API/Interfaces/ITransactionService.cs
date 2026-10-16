using Coiny.API.DTOs.Transactions;

namespace Coiny.API.Interfaces;

public interface ITransactionService
{
    Task<List<TransactionResponse>> GetTransactionsAsync();
    Task<TransactionPageResponse> GetPageAsync(TransactionQuery query);
    Task<TransactionResponse> CreateTransactionAsync(CreateTransactionRequest request);
}
