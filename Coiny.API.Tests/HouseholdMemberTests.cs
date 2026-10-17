using Coiny.API.Enums;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class HouseholdMemberTests
{
    [Fact]
    public async Task RosterIsHouseholdScopedAndIdentifiesTheCaller()
    {
        await using var db = await InvitationTestDatabase.Create();
        var roster = await db.Households("member").GetMembersAsync();
        Assert.Equal(2, roster.Count);
        Assert.Equal("member@coiny.test", Assert.Single(roster, m => m.IsCurrentUser).Email);
        Assert.DoesNotContain(roster, m => m.Email == "owner2@coiny.test");
        await Assert.ThrowsAsync<HouseholdMembershipRequiredException>(() => db.Households("new1").GetMembersAsync());
    }
    [Fact]
    public async Task RemovalRevokesInvitationAndPreservesUserAndFinancialData()
    {
        await using var db = await InvitationTestDatabase.Create();
        var code = (await db.Service().GenerateAsync()).Code;
        var context = db.Context();
        context.Accounts.Add(new Account { HouseholdId = 1, Name = "Checking", AccountType = AccountType.Checking,
            OpeningBalance = 150, OpeningBalanceDate = new(2026,10,1) }); await context.SaveChangesAsync();
        var member = await context.HouseholdMembers.AsNoTracking().SingleAsync(m => m.UserId == "member");
        await db.Households("owner").RemoveMemberAsync(member.Id);
        Assert.Null(await db.Households("member").GetCurrentHouseholdAsync());
        Assert.True(await context.Users.AnyAsync(u => u.Id == "member"));
        Assert.Equal(150, (await context.Accounts.AsNoTracking().SingleAsync()).OpeningBalance);
        Assert.False((await db.Service().GetStatusAsync()).HasActiveInvitation);
        await Assert.ThrowsAsync<InvalidInvitationException>(() => db.Service("member").RedeemAsync(code, 1));
        await Assert.ThrowsAsync<HouseholdMembershipRequiredException>(() => db.Households("member").GetMembersAsync());
    }
    [Fact]
    public async Task OnlyOwnersRemoveRegularMembersAndNoOwnerOrSelfCanBeRemoved()
    {
        await using var db = await InvitationTestDatabase.Create(); var context = db.Context();
        var member = await context.HouseholdMembers.SingleAsync(m => m.UserId == "member");
        var owner = await context.HouseholdMembers.SingleAsync(m => m.UserId == "owner");
        var foreign = await context.HouseholdMembers.SingleAsync(m => m.UserId == "owner2");
        await Assert.ThrowsAsync<HouseholdMemberPermissionException>(() => db.Households("member").RemoveMemberAsync(owner.Id));
        await Assert.ThrowsAsync<HouseholdMemberNotFoundException>(() => db.Households("owner").RemoveMemberAsync(foreign.Id));
        await Assert.ThrowsAsync<ProtectedHouseholdMemberException>(() => db.Households("owner").RemoveMemberAsync(owner.Id));
        member.Role = HouseholdRole.Owner; await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ProtectedHouseholdMemberException>(() => db.Households("owner").RemoveMemberAsync(member.Id));
    }
    [Fact]
    public async Task FailedInvitationRevocationRollsBackMembershipRemoval()
    {
        await using var db = await InvitationTestDatabase.Create(); await db.Service().GenerateAsync();
        var member = await db.Context().HouseholdMembers.AsNoTracking().SingleAsync(m => m.UserId == "member");
        await db.Context().Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectInvitationRevocation BEFORE UPDATE ON HouseholdInvitations
            BEGIN SELECT RAISE(ABORT, 'Simulated storage failure'); END;
            """);
        await Assert.ThrowsAsync<SqliteException>(() => db.Households("owner").RemoveMemberAsync(member.Id));
        Assert.NotNull(await db.Households("member").GetCurrentHouseholdAsync());
        Assert.True((await db.Service().GetStatusAsync()).HasActiveInvitation);
    }
}
