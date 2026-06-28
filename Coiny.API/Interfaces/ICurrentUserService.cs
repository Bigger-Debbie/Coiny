namespace Coiny.API.Interfaces;

public interface ICurrentUserService
{
    string? GetUserId();
    bool IsAuthenticated();
}