using System.Security.Claims;
using Coiny.API.Data;
using Coiny.API.Interfaces; 
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CoinyContext _context;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        CoinyContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public string? GetUserId()
    {
        return _httpContextAccessor
            .HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public async Task<int> GetCurrentHouseholdIdAsync()
    {
        var userId = GetUserId();

        if (userId is null)
            throw new UnauthorizedAccessException("User is not authenticated");

        var householdMember = await _context.HouseholdMembers
            .FirstOrDefaultAsync(hm => hm.UserId == userId);

        if (householdMember is null)
            throw new InvalidOperationException("User does not beling to a household");

        return householdMember.HouseholdId;
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor
            .HttpContext?
            .User
            .Identity?
            .IsAuthenticated ?? false;
    }
}