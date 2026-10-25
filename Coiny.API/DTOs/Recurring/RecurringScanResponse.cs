using System.ComponentModel.DataAnnotations;
namespace Coiny.API.DTOs.Recurring;
public record RecurringEvidence(int Id, DateOnly Date, decimal Amount, string AccountName, string Description);
public record RecurringBudgetTarget(Guid Key, string Name);
public record RecurringSuggestion(string Key, string Merchant, string Frequency, decimal TypicalAmount,
    decimal MinimumAmount, decimal MaximumAmount, DateOnly LastSeen, DateOnly NextExpected,
    int TransactionCount, string Status, Guid? AllocationKey, Guid? SuggestedAllocationKey,
    IReadOnlyList<RecurringEvidence> Transactions, bool Reviewed = false);
public record RecurringScanResponse(DateOnly FromDate, DateOnly ThroughDate, int TransactionsScanned,
    IReadOnlyList<RecurringSuggestion> Suggestions, IReadOnlyList<RecurringBudgetTarget> BudgetItems);
public class ReviewRecurringRequest
{
    [Required, StringLength(64, MinimumLength=64)] public string Key { get; set; } = string.Empty;
    [Required] public string Action { get; set; } = string.Empty;
    public Guid? AllocationKey { get; set; }
}
