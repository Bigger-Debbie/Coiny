using Coiny.API.Enums;

namespace Coiny.API.DTOs.Transactions;

public class CategoryBreakdownResponse
{
    public CategoryType CategoryType { get; init; }
    public decimal TotalAmount { get; init; }
    public int TransactionCount { get; init; }
    public IReadOnlyList<CategoryBreakdownItem> Categories { get; init; } = [];
}

public class CategoryBreakdownItem
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int TransactionCount { get; init; }
    public decimal Percentage { get; init; }
}
