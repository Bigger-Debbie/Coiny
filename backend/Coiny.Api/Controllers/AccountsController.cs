using System.Security.Claims;
using Coiny.Api.Data;
using Coiny.Api.DTOs.Accounts;
using Coiny.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coiny.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AccountsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAccount(CreateAccountRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var account = new Account
            {
                UserId = userId,
                Name = request.Name,
                InstitutionName = request.InstitutionName,
                Type = request.Type,
                OpeningBalance = request.OpeningBalance
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            var response = new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                InstitutionName = account.InstitutionName,
                Type = account.Type,
                OpeningBalance = account.OpeningBalance,
                IsActive = account.IsActive
            };

            return Ok(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetAccounts()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .Select(a => new AccountResponse
                {
                    Id = a.Id,
                    Name = a.Name,
                    InstitutionName = a.InstitutionName,
                    Type = a.Type,
                    OpeningBalance = a.OpeningBalance,
                    IsActive = a.IsActive
                })
                .ToListAsync();

            return Ok(accounts);
        }
    }
}
