namespace Coiny.API.Interfaces;

public interface IAccountBalanceService
{
    Task<decimal> GetCurrentBalanceAsync(int accountId);
}