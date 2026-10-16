using Coiny.API.Enums;

namespace Coiny.API.DTOs.Budgets;

public class BudgetRevisionResponse
{
    public int Id { get; init; }
    public int RevisionNumber { get; init; }
    public int EditVersion { get; init; }
    public DateOnly EffectiveFromMonth { get; init; }
    public BudgetRevisionState State { get; init; }
    public BudgetSetupStep SetupStep { get; init; }
    public DateTime? PublishedAt { get; init; }
    public IReadOnlyList<BudgetIncomeInput> IncomeSources { get; init; } = [];
    public IReadOnlyList<BudgetAllocationInput> Allocations { get; init; } = [];
}
