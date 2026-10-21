namespace Coiny.API.DTOs.Budgets;

public class BudgetIncomePeriodResponse
{
    public DateOnly AsOf { get; init; }
    public Guid? SourceKey { get; init; }
    public IReadOnlyList<BudgetIncomeSourceOption> Sources { get; init; } = [];
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public DateOnly? NextPayday { get; init; }
    public decimal UnpaidBillsTotal { get; init; }
    public decimal BillsTotal { get; init; }
    public decimal ContributionsTotal { get; init; }
    public decimal UnpaidContributionsTotal { get; init; }
    public int AwaitingTransactionCount { get; init; }
    public decimal AwaitingTransactionTotal { get; init; }
    public IReadOnlyList<BudgetScheduledEvent> Bills { get; init; } = [];
    public IReadOnlyList<BudgetScheduledEvent> Contributions { get; init; } = [];
}

public class BudgetIncomeSourceOption
{
    public Guid SourceKey { get; init; }
    public string Name { get; init; } = string.Empty;
}
