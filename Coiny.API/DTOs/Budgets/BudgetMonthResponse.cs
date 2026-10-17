using Coiny.API.Enums;

namespace Coiny.API.DTOs.Budgets;

public class BudgetMonthResponse
{
    public DateOnly Month { get; init; }
    public int? RevisionId { get; init; }
    public decimal HealthInsurance { get; init; }
    public decimal RetirementContributions { get; init; }
    public decimal ExpectedIncome { get; init; }
    public decimal Allocated { get; init; }
    public decimal Unassigned { get; init; }
    public decimal OverBudget { get; init; }
    public IReadOnlyList<BudgetMonthlyItem> IncomeSources { get; init; } = [];
    public IReadOnlyList<BudgetMonthlyItem> Allocations { get; init; } = [];
    public IReadOnlyList<BudgetScheduledEvent> Events { get; init; } = [];
}

public class BudgetMonthlyItem
{
    public Guid? SourceKey { get; init; }
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public BudgetAllocationType? AllocationType { get; init; }
    public decimal HealthInsurance { get; init; }
    public decimal RetirementContributions { get; init; }
    public decimal PlannedAmount { get; init; }
    public int OccurrenceCount { get; init; }
}

public class BudgetScheduledEvent
{
    public Guid? SourceKey { get; init; }
    public DateOnly Date { get; init; }
    public int ItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsIncome { get; init; }
    public BudgetAllocationType? AllocationType { get; init; }
    public decimal Amount { get; init; }
}
