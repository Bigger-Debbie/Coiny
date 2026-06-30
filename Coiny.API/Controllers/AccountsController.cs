using Coiny.API.Data.Configurations;
using Coiny.API.DTOs.Accounts;
using Coiny.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coiny.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController (IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountResponse>>> GetAccounts()
    {
        var accounts = await _accountService.GetAccountsAsync();

        return Ok(accounts);
    }

    [HttpPost]
    public async Task<ActionResult<AccountResponse>> CreateAccount(CreateAccountRequest request)
    {
        try
        {
            var account = await _accountService.CreateAccountAsync(request);

            return CreatedAtAction(
                nameof(GetAccounts),
                new { id = account.Id },
                account);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}