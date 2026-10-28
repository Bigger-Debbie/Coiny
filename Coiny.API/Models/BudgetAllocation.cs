using Coiny.API.Enums;

namespace Coiny.API.Models;

public class BudgetAllocation : BaseEntity
{
    public Guid SourceKey { get; set; } = Guid.NewGuid();
    public int BudgetRevisionId { get; set; }
    public BudgetRevision BudgetRevision { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public BudgetAllocationType AllocationType { get; set; }
    /// <summary>Monthly amount without a schedule; per-occurrence amount with one.</summary>
    public decimal Amount { get; set; }
    public string? SavingsPlanJson { get; set; }
    public BudgetSchedule? Schedule { get; set; }
}
