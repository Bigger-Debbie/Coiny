using System.Text.Json.Serialization;

namespace Coiny.API.DTOs.Budgets;

public class BudgetItemChange
{
    [JsonIgnore] public bool FundManaged { get; set; }
    [JsonIgnore] public Guid? NewSourceKey { get; set; }
    [JsonRequired] public int ExpectedRevisionId { get; set; }
    [JsonRequired] public DateOnly EffectiveDate { get; set; }
    [JsonRequired] public bool IsIncome { get; set; }
    public Guid? SourceKey { get; set; }
    public bool Remove { get; set; }
    public BudgetIncomeInput? Income { get; set; }
    public BudgetAllocationInput? Allocation { get; set; }
}

public record BudgetManagementResponse(BudgetRevisionResponse Revision, DateOnly EarliestEffectiveDate);
public record BudgetItemReview(BudgetMonthResponse Before, BudgetMonthResponse After, int ProtectedPayments, DateOnly EffectiveDate, BudgetIncomePeriodResponse BeforePeriod, BudgetIncomePeriodResponse AfterPeriod);
