using Coiny.API.DTOs.Budgets;

namespace Coiny.API.Interfaces;

public interface IBudgetService
{
    Task<BudgetManagementResponse> GetManagementAsync();
    Task<BudgetItemReview> ChangeItemAsync(BudgetItemChange request, bool preview);
    Task<BudgetIncomePeriodResponse> GetIncomePeriodAsync(DateOnly? asOf = null, Guid? sourceKey = null);
    Task<IReadOnlyList<BudgetIncomePeriodResponse>> GetIncomePeriodsAsync(DateOnly? asOf, int months);
    Task<BudgetSetupStatusResponse> GetSetupStatusAsync();
    Task<BudgetDraftPreviewResponse> PreviewDraftAsync(int draftId, int expectedEditVersion, DateOnly month);
    Task<BudgetRevisionResponse?> GetDraftAsync();
    Task<BudgetRevisionResponse> StartDraftAsync(DateOnly? effectiveFromMonth = null);
    Task<BudgetRevisionResponse> SaveDraftAsync(int draftId, BudgetDraftRequest request);
    Task<BudgetRevisionResponse> PublishDraftAsync(int draftId, int expectedEditVersion);
    Task<BudgetMonthResponse> GetMonthAsync(DateOnly month);
}
