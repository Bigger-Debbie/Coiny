using Coiny.API.DTOs.Transactions;

namespace Coiny.API.Interfaces;

public interface ITransferService
{
    Task<List<TransactionResponse>> GetCandidatesAsync(int transactionId, int accountId, int days);
    Task<TransferPairResponse> CreateCounterpartAsync(int transactionId, CreateTransferCounterpartRequest request);
    Task<TransferPairResponse> CreateAsync(CreateTransferRequest request);
    Task<TransactionResponse> CreateEntryAsync(CreateTransferEntryRequest request);
    Task<TransferPairResponse> LinkAsync(LinkTransferRequest request);
    Task UnlinkAsync(int transactionId, int? expectedEditVersion = null, int? expectedPairedEditVersion = null);
}
