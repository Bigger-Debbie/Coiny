using Coiny.API.DTOs.Transactions;

namespace Coiny.API.Interfaces;

public interface ITransactionService
{
    Task<List<TransactionResponse>> GetTransactionsAsync();
    Task<TransactionResponse> CreateTransactionAsync(CreateTransactionRequest request);
}