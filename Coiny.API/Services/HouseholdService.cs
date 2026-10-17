using Coiny.API.Data;
using Coiny.API.DTOs.Households;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class HouseholdMemberPermissionException() : InvalidOperationException("Only a household owner can remove members.");
public class HouseholdMemberNotFoundException() : InvalidOperationException("Member not found in your household.");
public class ProtectedHouseholdMemberException() : InvalidOperationException("You cannot remove yourself or a household owner.");

public class HouseholdService : IHouseholdService
{
    private readonly TimeProvider _clock;
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;

    public HouseholdService(
        CoinyContext context,
        ICurrentUserService currentUserService,
        TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<HouseholdMemberResponse>> GetMembersAsync()
    {
        var userId = _currentUserService.GetUserId() ?? throw new UnauthorizedAccessException("Sign in to continue.");
        var membership = await _context.HouseholdMembers.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == userId)
            ?? throw new HouseholdMembershipRequiredException();
        return await _context.HouseholdMembers.AsNoTracking().Where(m => m.HouseholdId == membership.HouseholdId)
            .OrderBy(m => m.Role).ThenBy(m => m.User.FirstName).ThenBy(m => m.Id)
            .Select(m => new HouseholdMemberResponse { Id = m.Id, FirstName = m.User.FirstName,
                Email = m.User.Email ?? "", Role = m.Role, IsCurrentUser = m.UserId == userId }).ToListAsync();
    }

    public async Task RemoveMemberAsync(int membershipId)
    {
        var userId = _currentUserService.GetUserId() ?? throw new UnauthorizedAccessException("Sign in to continue.");
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var owner = await _context.HouseholdMembers.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == userId && m.Role == HouseholdRole.Owner)
            ?? throw new HouseholdMemberPermissionException();
        var target = await _context.HouseholdMembers.AsNoTracking().SingleOrDefaultAsync(m => m.Id == membershipId && m.HouseholdId == owner.HouseholdId)
            ?? throw new HouseholdMemberNotFoundException();
        if (target.UserId == userId || target.Role == HouseholdRole.Owner) throw new ProtectedHouseholdMemberException();
        var deleted = await _context.HouseholdMembers.Where(m => m.Id == membershipId && m.HouseholdId == owner.HouseholdId
            && m.Role == HouseholdRole.Member && m.UserId != userId).ExecuteDeleteAsync();
        if (deleted != 1) throw new HouseholdMemberNotFoundException();
        var now = _clock.GetUtcNow().UtcTicks;
        await _context.HouseholdInvitations.Where(i => i.HouseholdId == owner.HouseholdId && i.ConsumedAtUtcTicks == null && i.RevokedAtUtcTicks == null)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.RevokedAtUtcTicks, now));
        await transaction.CommitAsync();
    }

    public async Task<HouseholdResponse?> GetCurrentHouseholdAsync()
    {
        var userId = _currentUserService.GetUserId();

        if (userId is null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        var householdMember = await _context.HouseholdMembers
            .Include(hm => hm.Household)
            .FirstOrDefaultAsync(hm => hm.UserId == userId);

        if (householdMember is null)
            return null;

        return new HouseholdResponse
        {
            Id = householdMember.Household.Id,
            Name = householdMember.Household.Name,
            Role = householdMember.Role
        };
    }

    public async Task<HouseholdResponse> CreateHouseholdAsync(CreateHouseholdRequest request)
    {
        var userId = _currentUserService.GetUserId();

        if (userId is null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            throw new ArgumentException("Enter a household name of 1–100 characters.");
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var alreadyHasHousehold = await _context.HouseholdMembers
            .AnyAsync(hm => hm.UserId == userId );

        if (alreadyHasHousehold)
            throw new HouseholdMembershipConflictException();

        var household = new Household
        {
            Name = request.Name.Trim()
        };

        var householdMember = new HouseholdMember
        {
            Household = household,
            UserId = userId,
            Role = HouseholdRole.Owner
        };

        _context.Households.Add(household);
        _context.HouseholdMembers.Add(householdMember);

        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 2067 })
        { throw new HouseholdMembershipConflictException(); }
        await transaction.CommitAsync();

        return new HouseholdResponse
        {
            Id = household.Id,
            Name = household.Name,
            Role = householdMember.Role
        };
    }
}