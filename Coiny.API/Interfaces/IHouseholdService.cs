using Coiny.API.DTOs.Households;

namespace Coiny.API.Interfaces;

public interface IHouseholdService
{
    Task<HouseholdResponse?> GetCurrentHouseholdAsync();
    Task<HouseholdResponse> CreateHouseholdAsync(CreateHouseholdRequest request);
}