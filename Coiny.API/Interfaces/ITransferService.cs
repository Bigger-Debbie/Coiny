using Coiny.API.DTOs.Transactions;

namespace Coiny.API.Interfaces;

public interface ITransferService
{
    Task<TransferPairResponse> CreateAsync(CreateTransferRequest request);
    Task<TransactionResponse> CreateEntryAsync(CreateTransferEntryRequest request);
    Task<TransferPairResponse> LinkAsync(LinkTransferRequest request);
    Task UnlinkAsync(int transactionId);
}
