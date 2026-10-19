using Coiny.API.Data;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;

public class BillPaymentService(CoinyContext context, ICurrentUserService user, IBudgetService budgets, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
    private async Task<BudgetScheduledEvent> Occurrence(Guid key, DateOnly date)
    {
        if (key == Guid.Empty || date == DateOnly.MinValue) throw new ArgumentException("Choose a scheduled bill occurrence.");
        var month = await budgets.GetMonthAsync(new(date.Year, date.Month, 1));
        return month.Events.SingleOrDefault(e => e.SourceKey == key && e.Date == date && !e.IsIncome && e.AllocationType is BudgetAllocationType.Housing or BudgetAllocationType.Bills)
            ?? throw new ArgumentException("This bill is not scheduled on that date in your household's published budget.");
    }
    public async Task<BillPaymentResponse> SaveAsync(Guid key, DateOnly date, BillPaymentRequest request)
    {
        var householdId = await user.GetCurrentHouseholdIdAsync();
        await using var dbTransaction = await context.Database.BeginTransactionAsync();
        await Occurrence(key, date);
        if (request.Note?.Length > 500) throw new ArgumentException("Notes must be at most 500 characters.");
        if (!request.IsPaid && request.TransactionId.HasValue) throw new ArgumentException("Detach the transaction when marking a bill unpaid.");
        var paidOn = request.PaidOn;
        if (request.TransactionId.HasValue)
        {
            var entry = await context.Transactions.Include(t => t.Category).SingleOrDefaultAsync(t => t.Id == request.TransactionId && t.HouseholdId == householdId);
            if (entry is null || entry.Category.CategoryType != CategoryType.Expense)
                throw new ArgumentException("Choose an expense transaction from your household.");
            if (await context.BillPayments.AnyAsync(p => p.TransactionId == entry.Id && !(p.HouseholdId == householdId && p.AllocationKey == key && p.DueDate == date)))
                throw new BudgetConflictException("This transaction is already linked to another bill.");
            paidOn ??= DateOnly.FromDateTime(entry.TransactionDate);
        }
        if (request.IsPaid && (paidOn ?? Today) > Today || paidOn == DateOnly.MinValue)
            throw new ArgumentException("Payment date must be a valid date on or before today.");
        var payment = await context.BillPayments.Include(p => p.Transaction).SingleOrDefaultAsync(p => p.HouseholdId == householdId && p.AllocationKey == key && p.DueDate == date);
        if (payment is null) { payment = new() { HouseholdId = householdId, AllocationKey = key, DueDate = date }; context.BillPayments.Add(payment); }
        payment.IsPaid = request.IsPaid; payment.PaidOn = request.IsPaid ? paidOn ?? Today : null;
        payment.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(); payment.TransactionId = request.TransactionId;
        try { await context.SaveChangesAsync(); await dbTransaction.CommitAsync(); }
        catch (DbUpdateException) { throw new BudgetConflictException("This bill or transaction changed. Reload the bill before saving again."); }
        return BillPaymentResponse.From(payment);
    }
    public async Task<IReadOnlyList<BillTransactionCandidate>> CandidatesAsync(Guid key, DateOnly date, string? search)
    {
        var householdId = await user.GetCurrentHouseholdIdAsync();
        var bill = await Occurrence(key, date);
        var query = context.Transactions.AsNoTracking().Where(t => t.HouseholdId == householdId && t.Category.CategoryType == CategoryType.Expense &&
            !context.BillPayments.Any(p => p.TransactionId == t.Id && !(p.HouseholdId == householdId && p.AllocationKey == key && p.DueDate == date)));
        if (string.IsNullOrWhiteSpace(search))
        {
            var from = date > DateOnly.MinValue.AddDays(45) ? date.AddDays(-45) : DateOnly.MinValue;
            var to = date < DateOnly.MaxValue.AddDays(-45) ? date.AddDays(45) : DateOnly.MaxValue;
            query = query.Where(t => t.TransactionDate >= from.ToDateTime(TimeOnly.MinValue) && t.TransactionDate <= to.ToDateTime(TimeOnly.MaxValue));
        }
        else
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(t => t.Description.ToLower().Contains(term) || (t.Merchant != null && t.Merchant.ToLower().Contains(term)) || t.Account.Name.ToLower().Contains(term) || t.Category.Name.ToLower().Contains(term));
        }
        var entries = await query.OrderByDescending(t => t.Amount == bill.Amount).ThenByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Take(50).Select(t => new BillTransactionCandidate(t.Id, DateOnly.FromDateTime(t.TransactionDate), t.Amount, t.Description, t.Account.Name, t.Category.Name, t.Amount == bill.Amount)).ToListAsync();
        return entries.OrderByDescending(t => t.ExactAmount).ThenBy(t => Math.Abs(t.Date.DayNumber - date.DayNumber)).ToList();
    }
}
