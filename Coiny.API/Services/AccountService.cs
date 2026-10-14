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
    private readonly IAccountBalanceService _accountBalanceService;
    private readonly IAccountBalanceSnapshotService _snapshotService;

    public AccountService(
        CoinyContext context,
        ICurrentUserService currentUserService,
        IAccountBalanceService accountBalanceService,
        IAccountBalanceSnapshotService snapshotService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _accountBalanceService = accountBalanceService;
        _snapshotService = snapshotService;
    }

    public async Task<List<AccountResponse>> GetAccountsAsync()
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        var accounts = await _context.Accounts
            .Where(a => a.HouseholdId == householdId)
            .Include(a => a.Institution)
            .OrderBy(a => a.Name)
            .ToListAsync();

        var response = new List<AccountResponse>();

        foreach (var account in accounts)
        {
            response.Add(new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                AccountType = account.AccountType,
                OpeningBalance = account.OpeningBalance,
                OpeningBalanceDate = account.OpeningBalanceDate,
                CurrentBalance = await _accountBalanceService
                    .GetCurrentBalanceAsync(account.Id),
                IsActive = account.IsActive,
                InstitutionId = account.InstitutionId,
                InstitutionName = account.Institution?.Name
            });
        }

        return response;
    }

    public async Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request)
    {
        if (!request.OpeningBalanceDate.HasValue || request.OpeningBalanceDate.Value == DateOnly.MinValue)
            throw new InvalidOperationException("A valid opening balance date is required.");

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
            OpeningBalanceDate = request.OpeningBalanceDate.Value,
            IsActive = true
        };

        await using var databaseTransaction = await _context.Database.BeginTransactionAsync();
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();
        await _snapshotService.RecalculateFromAsync(account.Id, account.OpeningBalanceDate);
        await _context.SaveChangesAsync();
        await databaseTransaction.CommitAsync();

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
            OpeningBalanceDate = account.OpeningBalanceDate,
            CurrentBalance = account.OpeningBalance,
            IsActive = account.IsActive,
            InstitutionId = account.InstitutionId,
            InstitutionName = institutionName
        };
    }
}
