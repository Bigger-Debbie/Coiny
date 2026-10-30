using System.Security.Cryptography;
using System.Text;
using Coiny.API.Data;
using Coiny.API.DTOs.Households;
using Coiny.API.DTOs.Invitations;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class InvitationPermissionException() : InvalidOperationException("Only a household owner can manage invitations.");
public class InvalidInvitationException() : InvalidOperationException("This join code is invalid or has expired. Ask the household owner for a new code.");
public class HouseholdMembershipConflictException() : InvalidOperationException("You already belong to a household.");

public class HouseholdInvitationService(IControlContext context, ICurrentUserService currentUser, TimeProvider clock) : IHouseholdInvitationService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private long Now => clock.GetUtcNow().UtcTicks;
    private string UserId => currentUser.GetUserId() ?? throw new UnauthorizedAccessException("Sign in to continue.");
    private async Task<HouseholdMember> Owner()
    {
        var id = UserId;
        return await context.HouseholdMembers.AsNoTracking().Include(m => m.Household)
            .SingleOrDefaultAsync(m => m.UserId == id && m.Role == HouseholdRole.Owner) ?? throw new InvitationPermissionException();
    }
    private async Task RequireNoHousehold()
    {
        var id = UserId;
        if (!await context.Users.AnyAsync(u => u.Id == id)) throw new UnauthorizedAccessException("Sign in to continue.");
        if (await context.HouseholdMembers.AnyAsync(m => m.UserId == id)) throw new HouseholdMembershipConflictException();
    }
    private static string Hash(string code)
    {
        if (code is null || code.Length > 32) throw new InvalidInvitationException();
        var normalized = code.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        if (normalized.Length != 8 || normalized.Any(c => !Alphabet.Contains(c))) throw new InvalidInvitationException();
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(normalized)));
    }
    private IQueryable<HouseholdInvitation> Active(long now) => context.HouseholdInvitations
        .Where(i => i.ExpiresAtUtcTicks > now && i.ConsumedAtUtcTicks == null && i.RevokedAtUtcTicks == null);

    public async Task<GeneratedInvitationResponse> GenerateAsync()
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        var owner = await Owner();
        var chars = Enumerable.Range(0, 8).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray();
        var code = new string(chars);
        var invitation = await context.HouseholdInvitations.SingleOrDefaultAsync(i => i.HouseholdId == owner.HouseholdId);
        if (invitation is null)
        {
            invitation = new() { HouseholdId = owner.HouseholdId };
            context.HouseholdInvitations.Add(invitation);
        }
        invitation.CodeHash = Hash(code);
        invitation.CreatedByUserId = owner.UserId;
        invitation.CreatedAt = clock.GetUtcNow().UtcDateTime;
        invitation.ExpiresAtUtcTicks = clock.GetUtcNow().AddMinutes(10).UtcTicks;
        invitation.ConsumedAtUtcTicks = null;
        invitation.RevokedAtUtcTicks = null;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return new() { Code = code[..4] + "-" + code[4..], HouseholdName = owner.Household.Name,
            ExpiresAt = new(invitation.ExpiresAtUtcTicks, TimeSpan.Zero) };
    }
    public async Task<InvitationStatusResponse> GetStatusAsync()
    {
        var owner = await Owner();
        var active = await Active(Now).AsNoTracking().SingleOrDefaultAsync(i => i.HouseholdId == owner.HouseholdId);
        return new() { HasActiveInvitation = active is not null,
            ExpiresAt = active is null ? null : new DateTimeOffset(active.ExpiresAtUtcTicks, TimeSpan.Zero) };
    }
    public async Task RevokeAsync()
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        var owner = await Owner();
        await context.HouseholdInvitations.Where(i => i.HouseholdId == owner.HouseholdId)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.RevokedAtUtcTicks, Now));
        await transaction.CommitAsync();
    }
    public async Task<InvitationPreviewResponse> PreviewAsync(string code)
    {
        await RequireNoHousehold();
        var hash = Hash(code);
        var invitation = await Active(Now).AsNoTracking().Include(i => i.Household).SingleOrDefaultAsync(i => i.CodeHash == hash)
            ?? throw new InvalidInvitationException();
        return new() { HouseholdId = invitation.HouseholdId, HouseholdName = invitation.Household.Name,
            ExpiresAt = new(invitation.ExpiresAtUtcTicks, TimeSpan.Zero) };
    }
    public async Task<HouseholdResponse> RedeemAsync(string code, int expectedHouseholdId)
    {
        var hash = Hash(code);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await RequireNoHousehold();
        var now = Now;
        var invitation = await Active(now).AsNoTracking().Include(i => i.Household)
            .SingleOrDefaultAsync(i => i.CodeHash == hash && i.HouseholdId == expectedHouseholdId)
            ?? throw new InvalidInvitationException();
        var consumedAt = Now;
        var consumed = await Active(consumedAt).Where(i => i.Id == invitation.Id && i.CodeHash == hash)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.ConsumedAtUtcTicks, consumedAt));
        if (consumed != 1) throw new InvalidInvitationException();
        context.HouseholdMembers.Add(new() { UserId = UserId, HouseholdId = invitation.HouseholdId, Role = HouseholdRole.Member });
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (DatabaseConflicts.IsUniqueViolation(ex))
        { throw new HouseholdMembershipConflictException(); }
        await transaction.CommitAsync();
        return new() { Id = invitation.HouseholdId, Name = invitation.Household.Name, Role = HouseholdRole.Member };
    }
}
