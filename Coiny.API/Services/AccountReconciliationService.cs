using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.DTOs.Accounts;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;

public class ReconciliationNotFoundException() : Exception("Account not found.");
public class ReconciliationConflictException() : Exception("The account changed after your check. Check the statement balance again before confirming.");
public class AccountReconciliationService(IFinancialContext context, ICurrentUserService user, TimeProvider clock, IControlContext? control = null)
{
    public static AccountReconciliationResponse Response(AccountReconciliation record) => new(record.Id, record.StatementDate, record.StatementBalance, DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc), record.ConfirmedByName, record.InvalidatedAt.HasValue, record.InvalidatedAt is { } invalidated ? DateTime.SpecifyKind(invalidated, DateTimeKind.Utc) : null, record.InvalidationReason);
    public async Task<IReadOnlyList<AccountReconciliationResponse>> HistoryAsync(int accountId)
    {
        await OwnedAccount(accountId);
        var records = await context.AccountReconciliations.AsNoTracking().Where(r => r.AccountId == accountId)
            .OrderByDescending(r => r.StatementDate).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(50).ToListAsync();
        return records.Select(Response).ToList();
    }
    public async Task<ReconciliationCheck> CheckAsync(int accountId, ReconciliationCheckRequest request)
    {
        var account = await OwnedAccount(accountId);
        if (request.StatementDate is not { } date || date == DateOnly.MinValue || date < account.OpeningBalanceDate || date > DateOnly.FromDateTime(clock.GetLocalNow().DateTime))
            throw new ArgumentException("Choose a statement date on or after the account opening date and on or before today.");
        if (request.StatementBalance is < -9999999999999999.99m or > 9999999999999999.99m || decimal.Round(request.StatementBalance, 2) != request.StatementBalance)
            throw new ArgumentException("Enter a statement balance with up to two decimal places.");
        var end = date.ToDateTime(TimeOnly.MaxValue);
        var entries = await context.Transactions.AsNoTracking().Include(t => t.Category).Where(t => t.AccountId == accountId && t.TransactionDate <= end).OrderBy(t => t.Id).ToListAsync();
        var balance = account.OpeningBalance + entries.Sum(t => AccountBalanceCalculator.GetBalanceChange(account.AccountType, t.Category.CategoryType, t.Amount, t.TransferDirection));
        var payload = JsonSerializer.Serialize(new
        {
            account.Id,
            account.AccountType,
            account.OpeningBalance,
            account.OpeningBalanceDate,
            StatementDate = date,
            Rows = entries.Select(t => new { t.Id, t.AccountId, t.TransactionDate, t.Amount, t.CategoryId, t.Category.CategoryType, t.TransferDirection, t.Description, t.Merchant, t.Notes, t.IsCleared })
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return new(date, request.StatementBalance, balance, request.StatementBalance - balance, request.StatementBalance == balance, fingerprint, entries.Count);
    }
    public async Task<AccountReconciliationResponse> ConfirmAsync(int accountId, ConfirmReconciliationRequest request)
    {
        if (!request.TransactionsReviewed) throw new ArgumentException("Confirm that you reviewed the statement transactions before reconciling.");
        await using var unit = await context.Database.BeginTransactionAsync();
        var check = await CheckAsync(accountId, request);
        if (check.LedgerFingerprint != request.ReviewedFingerprint) throw new ReconciliationConflictException();
        if (!check.Matches) throw new ArgumentException("The statement and Coiny balances must match before this checkpoint can be confirmed.");
        var actor = await (control ?? context as IControlContext ?? throw new InvalidOperationException("A control context is required.")).Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == user.GetUserId()) ?? throw new UnauthorizedAccessException();
        var existing = await context.AccountReconciliations.SingleOrDefaultAsync(r => r.AccountId == accountId && r.StatementDate == check.StatementDate && r.LedgerFingerprint == check.LedgerFingerprint && r.InvalidatedAt == null);
        if (existing is not null) return Response(existing);
        var account = await OwnedAccount(accountId);
        var record = new AccountReconciliation
        {
            HouseholdId = account.HouseholdId,
            AccountId = accountId,
            StatementDate = check.StatementDate,
            StatementBalance = check.StatementBalance,
            LedgerFingerprint = check.LedgerFingerprint,
            ConfirmedByUserId = actor.Id,
            ConfirmedByName = actor.FirstName?.Trim() is { Length: > 0 } name ? name : actor.Email ?? "Household member",
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        context.AccountReconciliations.Add(record); await context.SaveChangesAsync(); await unit.CommitAsync(); return Response(record);
    }
    private async Task<Account> OwnedAccount(int accountId)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        return await context.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId && a.HouseholdId == household) ?? throw new ReconciliationNotFoundException();
    }
}
