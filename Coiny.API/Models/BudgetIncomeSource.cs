namespace Coiny.API.Models;

public class BudgetIncomeSource : BaseEntity
{
    public int BudgetRevisionId { get; set; }
    public BudgetRevision BudgetRevision { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Expected take-home amount for each scheduled payday.</summary>
    public decimal Amount { get; set; }
    public BudgetSchedule Schedule { get; set; } = new();
}
