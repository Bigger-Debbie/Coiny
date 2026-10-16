using Coiny.API.DTOs.Budgets;

namespace Coiny.API.Interfaces;

public interface IBudgetService
{
    Task<BudgetSetupStatusResponse> GetSetupStatusAsync();
    Task<BudgetDraftPreviewResponse> PreviewDraftAsync(int draftId, int expectedEditVersion, DateOnly month);
    Task<BudgetRevisionResponse?> GetDraftAsync();
    Task<BudgetRevisionResponse> StartDraftAsync(DateOnly? effectiveFromMonth = null);
    Task<BudgetRevisionResponse> SaveDraftAsync(int draftId, BudgetDraftRequest request);
    Task<BudgetRevisionResponse> PublishDraftAsync(int draftId, int expectedEditVersion);
    Task<BudgetMonthResponse> GetMonthAsync(DateOnly month);
}
