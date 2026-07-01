using Coiny.API.Enums;

namespace Coiny.API.DTOs.Transactions;

public class TransactionResponse
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public CategoryType CategoryType { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public bool IsCleared { get; set; }
}