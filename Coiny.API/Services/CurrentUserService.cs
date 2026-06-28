using System.Security.Claims;
using Coiny.API.Interfaces; 

namespace Coiny.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetUserId()
    {
        return _httpContextAccessor
            .HttpContext?
            .User
            .FindFirstValue(ClaimTypes.NameIdentifier);
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