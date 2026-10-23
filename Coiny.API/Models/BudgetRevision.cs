using Coiny.API.Enums;

namespace Coiny.API.Models;

/// <summary>A complete plan version, rather than a set of changes to another version.</summary>
public class BudgetRevision : BaseEntity
{
    public int HouseholdBudgetId { get; set; }
    public HouseholdBudget HouseholdBudget { get; set; } = null!;
    public int RevisionNumber { get; set; }
    /// <summary>Incremented by draft saves/publication to reject stale edits.</summary>
    public int EditVersion { get; set; }
    /// <summary>The first calendar month to which this version applies.</summary>
    public DateOnly EffectiveFromMonth { get; set; }
    public DateOnly? EffectiveFromDate { get; set; }
    // Immutable planned occurrences already paid when this version was published.
    public string ProtectedPaymentsJson { get; set; } = "[]";
    public BudgetRevisionState State { get; set; } = BudgetRevisionState.Draft;
    public BudgetSetupStep SetupStep { get; set; } = BudgetSetupStep.Income;
    public DateTime? PublishedAt { get; set; }
    public ICollection<BudgetIncomeSource> IncomeSources { get; set; } = new List<BudgetIncomeSource>();
    public ICollection<BudgetAllocation> Allocations { get; set; } = new List<BudgetAllocation>();
}
