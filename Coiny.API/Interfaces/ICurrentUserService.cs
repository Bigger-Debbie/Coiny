namespace Coiny.API.Interfaces;

public interface ICurrentUserService
{
    string? GetUserId();
    Task<int> GetCurrentHouseholdIdAsync();
    bool IsAuthenticated();
}