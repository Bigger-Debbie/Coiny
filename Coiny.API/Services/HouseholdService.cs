using Coiny.API.Data;
using Coiny.API.DTOs.Households;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class HouseholdService : IHouseholdService
{
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;

    public HouseholdService(
        CoinyContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        var alreadyHasHousehold = await _context.HouseholdMembers
            .AnyAsync(hm => hm.UserId == userId );

        if (alreadyHasHousehold)
            throw new InvalidOperationException("User already belongs to a household");

        var household = new Household
        {
            Name = request.Name
        };

        var householdMember = new HouseholdMember
        {
            Household = household,
            UserId = userId,
            Role = HouseholdRole.Owner
        };

        _context.Households.Add(household);
        _context.HouseholdMembers.Add(householdMember);

        await _context.SaveChangesAsync();

        return new HouseholdResponse
        {
            Id = household.Id,
            Name = household.Name,
            Role = householdMember.Role
        };
    }
}