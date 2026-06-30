using Coiny.API.DTOs.Accounts;

namespace Coiny.API.Interfaces;

public interface IAccountService
{
    Task<List<AccountResponse>> GetAccountsAsync();
    Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request);
}