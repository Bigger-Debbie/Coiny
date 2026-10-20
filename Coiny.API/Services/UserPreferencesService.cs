using Coiny.API.Data;
using Coiny.API.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public record UserPreferences(bool IncludeMortgageInNetWorth);
public class UserPreferencesService(CoinyContext context, ICurrentUserService user)
{
    private async Task<Models.ApplicationUser> Current() => await context.Users.SingleOrDefaultAsync(u => u.Id == user.GetUserId()) ?? throw new UnauthorizedAccessException();
    public async Task<UserPreferences> GetAsync() => new((await Current()).IncludeMortgageInNetWorth);
    public async Task<UserPreferences> SaveAsync(bool includeMortgage)
    {
        var current = await Current(); current.IncludeMortgageInNetWorth = includeMortgage;
        await context.SaveChangesAsync(); return new(includeMortgage);
    }
}
