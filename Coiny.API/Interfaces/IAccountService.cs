using Coiny.API.DTOs.Accounts;

namespace Coiny.API.Interfaces;

public interface IAccountService
{
    Task<AccountResponse> UpdateAccountAsync(int id, CreateAccountRequest request);
    Task<List<AccountResponse>> GetAccountsAsync();
    Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request);
}