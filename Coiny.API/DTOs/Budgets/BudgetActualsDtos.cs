using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coiny.API.Enums;

namespace Coiny.API.DTOs.Budgets;

public class BudgetActualsResponse
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public DateOnly Month { get; init; }
    public DateOnly? ThroughDate { get; init; }
    public decimal Planned { get; init; }
    public decimal AssignedActual { get; init; }
    public decimal UnassignedExpenses { get; init; }
    public decimal AwaitingTransaction { get; init; }
    public IReadOnlyList<BudgetActualsItem> Items { get; init; } = [];
    public IReadOnlyList<BudgetCategoryDefault> CategoryDefaults { get; init; } = [];
    public IReadOnlyList<BudgetActualsTransaction> Transactions { get; init; } = [];
    public IReadOnlyList<BudgetComparisonPoint> Points { get; init; } = [];
    public int Page { get; init; }
    public int TotalCount { get; init; }
    public int PageSize { get; init; } = 10;
}

public record BudgetComparisonPoint(DateOnly Date, decimal PlannedSpending, decimal? ActualSpending,
    decimal PlannedContributions, decimal? ActualContributions);

public record BudgetActualsItem(Guid AllocationKey, string Name, BudgetAllocationType AllocationType,
    decimal Planned, decimal Actual, decimal Remaining, decimal AwaitingTransaction, int TransactionCount);
public record BudgetCategoryDefault(int CategoryId, string CategoryName, bool IsActive, Guid? AllocationKey,
    DateOnly? EffectiveFromMonth, int EditVersion);
public record BudgetActualsTransaction(int Id, int EditVersion, int? PairedEditVersion, DateOnly Date,
    string Description, decimal Amount, string AccountName, string CategoryName, CategoryType CategoryType,
    Guid? AllocationKey, string? AllocationName, string AssignmentSource, string? Merchant = null);

public class SaveBudgetCategoryRuleRequest
{
    [Required] public DateOnly? Month { get; set; }
    public Guid? AllocationKey { get; set; }
    [JsonRequired, Range(0, int.MaxValue)] public int ExpectedEditVersion { get; set; }
}
public class SaveTransactionBudgetAssignmentRequest
{
    [Required] public DateOnly? Month { get; set; }
    [JsonRequired] public bool UseCategoryDefault { get; set; }
    public Guid? AllocationKey { get; set; }
    [JsonRequired, Range(0, int.MaxValue)] public int ExpectedEditVersion { get; set; }
    [Range(0, int.MaxValue)] public int? ExpectedPairedEditVersion { get; set; }
}
