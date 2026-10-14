using Coiny.API.Data;
using Coiny.API.DTOs.Trends;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class AccountBalanceSnapshotService : IAccountBalanceSnapshotService
{
    private readonly CoinyContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AccountBalanceSnapshotService(CoinyContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task RecalculateFromAsync(int accountId, DateOnly affectedDate)
    {
        var account = await GetOwnedAccountAsync(accountId);

        if (affectedDate < account.OpeningBalanceDate)
            throw new InvalidOperationException("Snapshot date cannot be before the account's opening balance date.");

        // Writers hold a database transaction across the ledger and snapshot saves.
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Snapshot updates require an active database transaction.");

        var snapshots = await _context.AccountBalanceSnapshots
            .Where(snapshot => snapshot.AccountId == accountId && snapshot.SnapshotDate >= affectedDate)
            .OrderBy(snapshot => snapshot.SnapshotDate)
            .ToListAsync();

        if (snapshots.Count == 0 || snapshots[0].SnapshotDate != affectedDate)
        {
            var snapshot = new AccountBalanceSnapshot
            {
                AccountId = accountId,
                SnapshotDate = affectedDate
            };
            _context.AccountBalanceSnapshots.Add(snapshot);
            snapshots.Insert(0, snapshot);
        }

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.AccountId == accountId)
            .OrderBy(transaction => transaction.TransactionDate)
            .ThenBy(transaction => transaction.Id)
            .Select(transaction => new
            {
                transaction.TransactionDate,
                transaction.Amount,
                transaction.Category.CategoryType
            })
            .ToListAsync();

        var balance = account.OpeningBalance;
        var transactionIndex = 0;

        foreach (var snapshot in snapshots)
        {
            while (transactionIndex < transactions.Count &&
                DateOnly.FromDateTime(transactions[transactionIndex].TransactionDate) <= snapshot.SnapshotDate)
            {
                var transaction = transactions[transactionIndex++];
                balance += AccountBalanceCalculator.GetBalanceChange(
                    account.AccountType, transaction.CategoryType, transaction.Amount);
            }

            snapshot.Balance = balance;
        }
    }

    public async Task<IReadOnlyList<TrendDataPoint>> GetHistoryAsync(int accountId, DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));

        await GetOwnedAccountAsync(accountId);

        var query = _context.AccountBalanceSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.AccountId == accountId);

        var baseline = await query
            .Where(snapshot => snapshot.SnapshotDate < startDate)
            .OrderByDescending(snapshot => snapshot.SnapshotDate)
            .Select(snapshot => new TrendDataPoint { Date = snapshot.SnapshotDate, Value = snapshot.Balance })
            .FirstOrDefaultAsync();

        var points = await query
            .Where(snapshot => snapshot.SnapshotDate >= startDate && snapshot.SnapshotDate <= endDate)
            .OrderBy(snapshot => snapshot.SnapshotDate)
            .Select(snapshot => new TrendDataPoint { Date = snapshot.SnapshotDate, Value = snapshot.Balance })
            .ToListAsync();

        if (baseline is not null)
            points.Insert(0, baseline);

        return points.AsReadOnly();
    }

    private async Task<Account> GetOwnedAccountAsync(int accountId)
    {
        var householdId = await _currentUserService.GetCurrentHouseholdIdAsync();

        return await _context.Accounts.FirstOrDefaultAsync(account =>
            account.Id == accountId && account.HouseholdId == householdId)
            ?? throw new InvalidOperationException("Account does not belong to the current household.");
    }
}
