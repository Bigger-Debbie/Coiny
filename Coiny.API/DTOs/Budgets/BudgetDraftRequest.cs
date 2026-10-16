using Coiny.API.Enums;
using System.Text.Json.Serialization;

namespace Coiny.API.DTOs.Budgets;

public class BudgetDraftRequest
{
    [JsonRequired]
    public int ExpectedEditVersion { get; set; }
    [JsonRequired]
    public DateOnly EffectiveFromMonth { get; set; }
    [JsonRequired]
    public BudgetSetupStep SetupStep { get; set; }
    [JsonRequired]
    public List<BudgetIncomeInput> IncomeSources { get; set; } = [];
    [JsonRequired]
    public List<BudgetAllocationInput> Allocations { get; set; } = [];
}

public class BudgetScheduleInput
{
    [JsonRequired]
    public BudgetFrequency Frequency { get; set; }
    [JsonRequired]
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int? DayOfMonth { get; set; }
    public int? SecondDayOfMonth { get; set; }
}

public class BudgetIncomeInput
{
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    [JsonRequired]
    public decimal Amount { get; set; }
    [JsonRequired]
    public BudgetScheduleInput Schedule { get; set; } = new();
}

public class BudgetAllocationInput
{
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    [JsonRequired]
    public decimal Amount { get; set; }
    [JsonRequired]
    public BudgetAllocationType AllocationType { get; set; }
    public BudgetScheduleInput? Schedule { get; set; }
}
