using Coiny.API.Enums;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class HouseholdInvitationTests
{
    [Fact]
    public async Task OwnerGeneratesHashedCodeAndPreviewDoesNotConsumeIt()
    {
        await using var db = await InvitationTestDatabase.Create();
        var invitation = await db.Service().GenerateAsync();
        Assert.Matches("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$", invitation.Code);
        Assert.Equal(db.Clock.Now.AddMinutes(10), invitation.ExpiresAt);
        var stored = await db.Context().HouseholdInvitations.AsNoTracking().SingleAsync();
        Assert.Equal(64, stored.CodeHash.Length); Assert.DoesNotContain(invitation.Code.Replace("-", ""), stored.CodeHash);
        var normalized = " " + invitation.Code.ToLowerInvariant().Replace("-", " ") + " ";
        for (var i = 0; i < 2; i++) {
            var preview = await db.Service("new1").PreviewAsync(normalized);
            Assert.Equal("Ross household", preview.HouseholdName); Assert.Equal(1, preview.HouseholdId);
        }
        Assert.True((await db.Service().GetStatusAsync()).HasActiveInvitation);
        var joined = await db.Service("new1").RedeemAsync(invitation.Code, 1);
        Assert.Equal(HouseholdRole.Member, joined.Role);
        Assert.False((await db.Service().GetStatusAsync()).HasActiveInvitation);
        await Assert.ThrowsAsync<InvalidInvitationException>(() => db.Service("new2").RedeemAsync(invitation.Code, 1));
        Assert.Empty(await db.Context().Transactions.ToListAsync());
    }
    [Fact]
    public async Task RotationRevocationExpiryAndExpectedHouseholdAreEnforced()
    {
        await using var db = await InvitationTestDatabase.Create();
        var old = await db.Service().GenerateAsync(); var current = await db.Service().GenerateAsync();
        await Assert.ThrowsAsync<InvalidInvitationException>(() => db.Service("new1").PreviewAsync(old.Code));
        await Assert.ThrowsAsync<InvalidInvitationException>(() => db.Service("new1").RedeemAsync(current.Code, 2));
        Assert.True((await db.Service().GetStatusAsync()).HasActiveInvitation);
        await db.Service().RevokeAsync(); await db.Service().RevokeAsync();
        await Assert.ThrowsAsync<InvalidInvitationException>(() => db.Service("new1").PreviewAsync(current.Code));
        var fresh = await db.Service().GenerateAsync(); db.Clock.Now = fresh.ExpiresAt;
        await Assert.ThrowsAsync<InvalidInvitationException>(() => db.Service("new1").RedeemAsync(fresh.Code, 1));
        Assert.False((await db.Service().GetStatusAsync()).HasActiveInvitation);
        Assert.Single(await db.Context().HouseholdInvitations.ToListAsync());
    }
    [Fact]
    public async Task MembersAndOtherHouseholdOwnersCannotManageAnotherHouseholdsCode()
    {
        await using var db = await InvitationTestDatabase.Create();
        await db.Service().GenerateAsync();
        await Assert.ThrowsAsync<InvitationPermissionException>(() => db.Service("member").GenerateAsync());
        await Assert.ThrowsAsync<InvitationPermissionException>(() => db.Service("member").RevokeAsync());
        await Assert.ThrowsAsync<InvitationPermissionException>(() => db.Service("new1").GetStatusAsync());
        await db.Service("owner2").RevokeAsync();
        Assert.True((await db.Service().GetStatusAsync()).HasActiveInvitation);
        await Assert.ThrowsAsync<HouseholdMembershipConflictException>(() => db.Service("owner2").PreviewAsync("ABCD-EFGH"));
    }
    [Fact]
    public async Task ConcurrentRedemptionAdmitsExactlyOnePerson()
    {
        await using var db = await InvitationTestDatabase.Create(); var code = (await db.Service().GenerateAsync()).Code;
        async Task<bool> Join(string user) {
            try { await db.Service(user).RedeemAsync(code, 1); return true; }
            catch (InvalidInvitationException) { return false; }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => Join("new1")), Task.Run(() => Join("new2")));
        Assert.Single(outcomes, success => success);
        Assert.Single(await db.Context().HouseholdMembers.Where(m => m.UserId == "new1" || m.UserId == "new2").ToListAsync());
    }
    [Fact]
    public async Task ConcurrentCreateAndJoinCannotGiveOneUserTwoHouseholds()
    {
        await using var db = await InvitationTestDatabase.Create(); var code = (await db.Service().GenerateAsync()).Code;
        async Task<bool> Create() {
            try { await db.Households("new1").CreateHouseholdAsync(new() { Name = "New home" }); return true; }
            catch (HouseholdMembershipConflictException) { return false; }
        }
        async Task<bool> Join() {
            try { await db.Service("new1").RedeemAsync(code, 1); return true; }
            catch (HouseholdMembershipConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Task.Run(Create), Task.Run(Join));
        Assert.Single(outcomes, success => success);
        Assert.Single(await db.Context().HouseholdMembers.Where(m => m.UserId == "new1").ToListAsync());
        Assert.Equal(outcomes[0], (await db.Service().GetStatusAsync()).HasActiveInvitation);
    }
    [Fact]
    public async Task FailedMembershipInsertRollsBackCodeConsumption()
    {
        await using var db = await InvitationTestDatabase.Create(); var code = (await db.Service().GenerateAsync()).Code;
        await db.Context().Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectInvitationJoin BEFORE INSERT ON HouseholdMembers
            WHEN NEW.UserId = 'new1' BEGIN SELECT RAISE(ABORT, 'Simulated storage failure'); END;
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.Service("new1").RedeemAsync(code, 1));
        Assert.True((await db.Service().GetStatusAsync()).HasActiveInvitation);
        Assert.Equal(1, (await db.Service("new2").RedeemAsync(code, 1)).Id);
    }
}
