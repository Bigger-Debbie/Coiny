using Coiny.API.Data.Configurations;
using Coiny.API.DTOs.Accounts;
using Coiny.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Coiny.API.DTOs.Trends;

namespace Coiny.API.Controllers;

[ApiController]
[Authorize]
[BudgetApiExceptionFilter]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly IAccountTrendService _trendService;

    public AccountsController (IAccountService accountService, IAccountTrendService trendService)
    {
        _accountService = accountService;
        _trendService = trendService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountResponse>>> GetAccounts()
    {
        var accounts = await _accountService.GetAccountsAsync();

        return Ok(accounts);
    }

    [HttpGet("{accountId:int}/trend")]
    public async Task<ActionResult<TrendResult>> GetTrend(int accountId,
        [FromQuery, Required] DateOnly? startDate, [FromQuery, Required] DateOnly? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
            return BadRequest("Start date and end date are required.");
        try
        {
            return Ok(await _trendService.GetTrendAsync(startDate.Value, endDate.Value, [accountId]));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:int:min(1)}")]
    public async Task<ActionResult<AccountResponse>> UpdateAccount(int id, CreateAccountRequest request)
    {
        try { return Ok(await _accountService.UpdateAccountAsync(id, request)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
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
