namespace Coiny.API.DTOs.Budgets;

public class BudgetSetupStatusResponse
{
    public DateOnly CurrentMonth { get; set; }
    public bool HasPublishedBudget { get; set; }
    public DateOnly? FirstPublishedMonth { get; set; }
    public int? CurrentRevisionId { get; set; }
    public BudgetRevisionResponse? Draft { get; set; }
}

public class BudgetDraftPreviewResponse
{
    public int DraftId { get; set; }
    public int EditVersion { get; set; }
    public BudgetMonthResponse Month { get; set; } = new();
}
