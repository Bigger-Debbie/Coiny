using Coiny.API.Models;

namespace Coiny.API.Interfaces;

public interface ITokenService
{
    string CreateToken(ApplicationUser user);
}