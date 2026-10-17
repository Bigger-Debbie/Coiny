using System.ComponentModel.DataAnnotations;

namespace Coiny.API.DTOs.Invitations;

public class InvitationCodeRequest
{
    [Required, StringLength(32)]
    public string Code { get; set; } = string.Empty;
}
public class RedeemInvitationRequest : InvitationCodeRequest
{
    [Required, Range(1, int.MaxValue)]
    public int? HouseholdId { get; set; }
}
public class GeneratedInvitationResponse
{
    public string Code { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string HouseholdName { get; init; } = string.Empty;
}
public class InvitationStatusResponse
{
    public bool HasActiveInvitation { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
public class InvitationPreviewResponse
{
    public int HouseholdId { get; init; }
    public string HouseholdName { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}
