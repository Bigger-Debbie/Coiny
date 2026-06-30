using System.Runtime.CompilerServices;
using Coiny.API.Data;
using Coiny.API.DTOs.Accounts;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class AccountService : IAccountService
{
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AccountService( CoinyContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<AccountResponse>> GetAccountsAsync()
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        return await _context.Accounts
            .Where(a => a.HouseholdId == householdId)
            .Include(a => a.Institution)
            .OrderBy(a => a.Name)
            .Select(a => new AccountResponse
            {
                Id = a.Id,
                Name = a.Name,
                AccountType = a.AccountType,
                OpeningBalance = a.OpeningBalance,
                CurrentBalance = a.OpeningBalance,
                IsActive = a.IsActive,
                InstitutionId = a.InstitutionId,
                InstitutionName = a.Institution != null
                    ? a.Institution.Name
                    : null
            })
            .ToListAsync();
    }

    public async Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request)
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        if (request.InstitutionId.HasValue)
        {
            var institutionBelongsToHousehold = await _context.Institutions
                .AnyAsync(i => 
                    i.Id == request.InstitutionId.Value &&
                    i.HouseholdId == householdId);

            if (!institutionBelongsToHousehold)
                throw new InvalidOperationException("Institution does not belong to the current hosuehold");
        }

        var account = new Account
        {
            HouseholdId = householdId,
            InstitutionId = request.InstitutionId,
            Name = request.Name,
            AccountType = request.AccountType,
            OpeningBalance = request.OpeningBalance,
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        string? institutionName = null;

        if (account.InstitutionId.HasValue)
        {
            institutionName = await _context.Institutions
                .Where(i => i.Id == account.InstitutionId.Value)
                .Select(i => i.Name)
                .FirstOrDefaultAsync();
        }

        return new AccountResponse
        {
            Id = account.Id,
            Name = account.Name,
            AccountType = account.AccountType,
            OpeningBalance = account.OpeningBalance,
            CurrentBalance = account.OpeningBalance,
            IsActive = account.IsActive,
            InstitutionId = account.InstitutionId,
            InstitutionName = institutionName
        };
    }
}