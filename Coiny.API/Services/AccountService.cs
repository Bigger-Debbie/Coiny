using System.Runtime.CompilerServices;
using Coiny.API.Enums;
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

    public async Task<AccountResponse> UpdateAccountAsync(int id, CreateAccountRequest request)
    {
        var household = await _currentUserService.GetCurrentHouseholdIdAsync();
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var account = await _context.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.HouseholdId == household)
            ?? throw new KeyNotFoundException("Account not found.");
        var name = request.Name.Trim();
        if (name.Length is < 1 or > 100) throw new ArgumentException("Enter an account name of up to 100 characters.");
        if (request.AccountType != account.AccountType) throw new ArgumentException("Account type cannot be changed after creation.");
        if (request.LoanSubtype.HasValue && (!Enum.IsDefined(request.LoanSubtype.Value) || account.AccountType != AccountType.Loan))
            throw new ArgumentException("Choose a valid loan subtype, only for a loan account.");
        if (request.OpeningBalance < 0 || decimal.Round(request.OpeningBalance, 2) != request.OpeningBalance)
            throw new ArgumentException("Enter an opening balance of zero or more with up to two decimal places.");
        if (request.OpeningBalanceDate is not { } date || date == DateOnly.MinValue || date > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Choose a valid opening date on or before today.");
        if (await _context.Transactions.AnyAsync(t => t.AccountId == id && t.TransactionDate < date.ToDateTime(TimeOnly.MinValue)))
            throw new ArgumentException("The opening date cannot be after an existing transaction. Choose an earlier date.");
        if (request.InstitutionId.HasValue && !await _context.Institutions.AnyAsync(i => i.Id == request.InstitutionId && i.HouseholdId == household))
            throw new ArgumentException("Institution does not belong to the current household.");
        var rebuild = account.OpeningBalance != request.OpeningBalance || account.OpeningBalanceDate != date;
        account.Name = name; account.InstitutionId = request.InstitutionId;
        account.LoanSubtype = account.AccountType == AccountType.Loan ? request.LoanSubtype ?? LoanSubtype.Other : null;
        account.OpeningBalance = request.OpeningBalance; account.OpeningBalanceDate = date;
        if (rebuild)
        {
            var earlier = await _context.AccountBalanceSnapshots.Where(s => s.AccountId == id && s.SnapshotDate < date).ToListAsync();
            _context.AccountBalanceSnapshots.RemoveRange(earlier);
        }
        await _context.SaveChangesAsync();
        if (rebuild) { await _snapshotService.RecalculateFromAsync(id, date); await _context.SaveChangesAsync(); }
        await transaction.CommitAsync();
        return (await GetAccountsAsync()).Single(a => a.Id == id);
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
                LatestReconciliation = (await _context.AccountReconciliations.AsNoTracking().Where(r => r.AccountId == account.Id)
                    .OrderByDescending(r => r.StatementDate).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync()) is { } reconciliation
                    ? AccountReconciliationService.Response(reconciliation) : null,
                Id = account.Id,
                Name = account.Name,
                AccountType = account.AccountType,
                LoanSubtype = account.LoanSubtype,
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

        if (!Enum.IsDefined(request.AccountType) || (request.LoanSubtype.HasValue && (!Enum.IsDefined(request.LoanSubtype.Value) || request.AccountType != AccountType.Loan)))
            throw new InvalidOperationException("Choose a valid account type and use loan subtypes only for loans.");
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
            LoanSubtype = request.AccountType == AccountType.Loan ? request.LoanSubtype ?? LoanSubtype.Other : null,
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
            LoanSubtype = account.LoanSubtype,
            OpeningBalance = account.OpeningBalance,
            OpeningBalanceDate = account.OpeningBalanceDate,
            CurrentBalance = account.OpeningBalance,
            IsActive = account.IsActive,
            InstitutionId = account.InstitutionId,
            InstitutionName = institutionName
        };
    }
}
