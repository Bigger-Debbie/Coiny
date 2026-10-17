using Coiny.API.DTOs.Households;

namespace Coiny.API.Interfaces;

public interface IHouseholdService
{
    Task<IReadOnlyList<HouseholdMemberResponse>> GetMembersAsync();
    Task RemoveMemberAsync(int membershipId);
    Task<HouseholdResponse?> GetCurrentHouseholdAsync();
    Task<HouseholdResponse> CreateHouseholdAsync(CreateHouseholdRequest request);
}