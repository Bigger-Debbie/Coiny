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
        if (key == Guid.Empty || date == DateOnly.MinValue) throw new ArgumentException("Choose a scheduled bill or contribution occurrence.");
        var month = await budgets.GetMonthAsync(new(date.Year, date.Month, 1));
        var occurrence = month.Events.SingleOrDefault(e => e.SourceKey == key && e.Date == date && !e.IsIncome &&
            e.AllocationType is BudgetAllocationType.Housing or BudgetAllocationType.Bills or BudgetAllocationType.Subscriptions or BudgetAllocationType.Savings or BudgetAllocationType.Investments);
        occurrence ??= (await budgets.GetIncomePeriodAsync(date)).Contributions.SingleOrDefault(e => e.IsPeriodSetAside && e.SourceKey == key && e.Date == date);
        return occurrence ?? throw new ArgumentException("This bill or contribution is not scheduled on that date in your household's published budget.");
    }
    public async Task<BillPaymentResponse> SaveAsync(Guid key, DateOnly date, BillPaymentRequest request)
    {
        var householdId = await user.GetCurrentHouseholdIdAsync();
        await using var dbTransaction = await context.Database.BeginTransactionAsync();
        var occurrence = await Occurrence(key, date);
        var contribution = occurrence.AllocationType is BudgetAllocationType.Savings or BudgetAllocationType.Investments;
        if (request.Note?.Length > 500) throw new ArgumentException("Notes must be at most 500 characters.");
        var selected = request.TransactionIds ?? (request.TransactionId is int legacyId ? new[] { legacyId } : []);
        if (selected.Length > 50 || selected.Distinct().Count() != selected.Length || selected.Any(id => id <= 0))
            throw new ArgumentException("Choose up to 50 different transactions.");
        if (request.TransactionIds is not null && request.TransactionId is int primary && !selected.Contains(primary))
            throw new ArgumentException("The primary transaction must be included in the selected transactions.");
        var entries = new List<Transaction>();
        if (!request.IsPaid && selected.Length > 0) throw new ArgumentException("Detach the transaction when marking an occurrence unpaid.");
        if(request.IsSkipped && (occurrence.AllocationType!=BudgetAllocationType.Savings || request.IsPaid || selected.Length>0))
            throw new ArgumentException("Only an unlinked savings contribution can be skipped. Recorded transactions remain in actuals.");
        var paidOn = request.PaidOn;
        foreach (var selectedId in selected)
        {
            var entry = await context.Transactions.Include(t => t.Category).Include(t => t.PairedTransaction).SingleOrDefaultAsync(t => t.Id == selectedId && t.HouseholdId == householdId);
            if (entry is null || !(entry.Category.CategoryType == CategoryType.Expense || contribution && entry.Category.CategoryType == CategoryType.Transfer))
                throw new ArgumentException(contribution ? "Choose an expense or transfer transaction from your household." : "Choose an expense transaction from your household.");
            if (entry.PairedTransactionId is int paired && selected.Contains(paired))
                throw new ArgumentException("Choose only one side of each transfer.");
            entries.Add(entry);
            if (await context.TransactionBudgetAssignments.AnyAsync(a => a.AllocationKey != null && (a.TransactionId == entry.Id || a.TransactionId == entry.PairedTransactionId)))
                throw new BudgetConflictException("Remove this movement's individual budget assignment before linking it to a scheduled payment.");
            if (await context.BillPayments.AnyAsync(p => (p.TransactionId == entry.Id || entry.PairedTransactionId.HasValue && p.TransactionId == entry.PairedTransactionId || p.Links.Any(l => l.TransactionId == entry.Id || l.TransactionId == entry.PairedTransactionId)) &&
                !(p.HouseholdId == householdId && p.AllocationKey == key && p.DueDate == date)))
                throw new BudgetConflictException("This transaction or its linked transfer is already assigned to another bill or contribution.");
            // Linking replaces a deliberate unassigned preference for either side of this movement.
            var unassigned = await context.TransactionBudgetAssignments.Where(a => a.AllocationKey == null &&
                (a.TransactionId == entry.Id || a.TransactionId == entry.PairedTransactionId)).ToListAsync();
            context.TransactionBudgetAssignments.RemoveRange(unassigned);
            paidOn ??= DateOnly.FromDateTime(entry.TransactionDate);
        }
        if (request.IsPaid && (paidOn ?? Today) > Today || paidOn == DateOnly.MinValue)
            throw new ArgumentException("Payment date must be a valid date on or before today.");
        var payment = await context.BillPayments.Include(p => p.Transaction).ThenInclude(t => t!.PairedTransaction).Include(p => p.Links).ThenInclude(l => l.Transaction).ThenInclude(t => t.PairedTransaction).SingleOrDefaultAsync(p => p.HouseholdId == householdId && p.AllocationKey == key && p.DueDate == date);
        if (payment is null) { payment = new() { HouseholdId = householdId, AllocationKey = key, DueDate = date }; context.BillPayments.Add(payment); }
        var wasSkipped=payment.IsSkipped;
        if(!request.IsSkipped && occurrence.AllocationType==BudgetAllocationType.Savings)
        {
            var restores=await context.BillPayments.Where(p=>p.HouseholdId==householdId && p.AllocationKey==key && p.IsSkipped && p.SkippedPeriodStart<=date && p.SkippedPeriodEnd>=date).ToListAsync();
            wasSkipped=wasSkipped || restores.Count>0;
            foreach(var skipped in restores){skipped.IsSkipped=false;skipped.SkippedPeriodStart=null;skipped.SkippedPeriodEnd=null;}
        }
        if(request.IsSkipped && (payment.TransactionId.HasValue || payment.Links.Count>0))throw new ArgumentException("Unlink this contribution's transactions before skipping it. Actual money already recorded is not removed.");
        if(request.IsSkipped)
        {
            var period=await budgets.GetIncomePeriodAsync(date);
            if(period.StartDate is null || period.EndDate is null)throw new ArgumentException("A complete income period is required to skip a savings contribution.");
            payment.SkippedPeriodStart=period.StartDate;payment.SkippedPeriodEnd=period.EndDate;
        }
        else {payment.SkippedPeriodStart=null;payment.SkippedPeriodEnd=null;}
        payment.IsSkipped=request.IsSkipped;
        payment.IsPaid = request.IsPaid; payment.PaidOn = request.IsPaid ? paidOn ?? Today : null;
        payment.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(); payment.TransactionId = selected.Cast<int?>().FirstOrDefault();
        payment.Transaction = entries.FirstOrDefault();
        var additional = selected.Skip(1).ToHashSet();
        foreach (var link in payment.Links.Where(l => !additional.Contains(l.TransactionId)).ToList()) payment.Links.Remove(link);
        foreach (var entry in entries.Skip(1).Where(t => !payment.Links.Any(l => l.TransactionId == t.Id)))
            payment.Links.Add(new() { TransactionId = entry.Id, Transaction = entry });
        if(!request.IsSkipped && !wasSkipped) await SavingsFundsService.SynchronizePaymentAsync(context,householdId,key,date,payment,entries,Today);
        try { await context.SaveChangesAsync(); await dbTransaction.CommitAsync(); }
        catch (DbUpdateException) { throw new BudgetConflictException("This scheduled item or transaction changed. Reload it before saving again."); }
        return BillPaymentResponse.From(payment);
    }
    public async Task<IReadOnlyList<BillTransactionCandidate>> CandidatesAsync(Guid key, DateOnly date, string? search)
    {
        var householdId = await user.GetCurrentHouseholdIdAsync();
        var bill = await Occurrence(key, date);
        var contribution = bill.AllocationType is BudgetAllocationType.Savings or BudgetAllocationType.Investments;
        var query = context.Transactions.AsNoTracking().Where(t => t.HouseholdId == householdId &&
            (t.Category.CategoryType == CategoryType.Expense || contribution && t.Category.CategoryType == CategoryType.Transfer) &&
            !context.TransactionBudgetAssignments.Any(a => a.AllocationKey != null && (a.TransactionId == t.Id || a.TransactionId == t.PairedTransactionId)) &&
            !context.BillPayments.Any(p => (p.TransactionId == t.Id || t.PairedTransactionId.HasValue && p.TransactionId == t.PairedTransactionId || p.Links.Any(l => l.TransactionId == t.Id || l.TransactionId == t.PairedTransactionId)) &&
                !(p.HouseholdId == householdId && p.AllocationKey == key && p.DueDate == date)));
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
            .Take(50).Select(t => new BillTransactionCandidate(t.Id, DateOnly.FromDateTime(t.TransactionDate), t.Amount, t.Description, t.Account.Name, t.Category.Name, t.Amount == bill.Amount,
                t.Category.CategoryType, t.TransferDirection)).ToListAsync();
        return entries.OrderByDescending(t => t.ExactAmount).ThenBy(t => Math.Abs(t.Date.DayNumber - date.DayNumber)).ToList();
    }
}
