using Coiny.API.DTOs.Households;
using Coiny.API.DTOs.Invitations;

namespace Coiny.API.Interfaces;

public interface IHouseholdInvitationService
{
    Task<GeneratedInvitationResponse> GenerateAsync();
    Task<InvitationStatusResponse> GetStatusAsync();
    Task RevokeAsync();
    Task<InvitationPreviewResponse> PreviewAsync(string code);
    Task<HouseholdResponse> RedeemAsync(string code, int expectedHouseholdId);
}
