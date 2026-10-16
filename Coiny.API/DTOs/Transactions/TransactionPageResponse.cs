namespace Coiny.API.DTOs.Transactions;

public class TransactionPageResponse
{
    public List<TransactionResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
