using Coiny.API.Data;
using Coiny.API.DTOs.Recurring;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;
public class RecurringExpenseService(IFinancialContext context, ICurrentUserService user, IBudgetService budgets, TimeProvider clock)
{
    public async Task<RecurringScanResponse> ScanAsync()
    { return await ScanHouseholdAsync(await user.GetCurrentHouseholdIdAsync(), budgets); }
    internal async Task<RecurringScanResponse> ScanHouseholdAsync(int household, IBudgetService householdBudgets)
    {
        var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
        var from=today.AddMonths(-30); var start=from.ToDateTime(TimeOnly.MinValue); var end=today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows=await context.Transactions.AsNoTracking().Where(t=>t.HouseholdId==household && t.Category.CategoryType==CategoryType.Expense && t.Amount>0 && t.TransactionDate>=start && t.TransactionDate<end)
            .Select(t=>new { t.Id,t.Merchant,t.Description,t.Amount,t.TransactionDate,AccountName=t.Account.Name }).ToListAsync();
        var patterns=RecurringExpenseDetector.Detect(rows.Select(t=>new RecurringInput(t.Id,string.IsNullOrWhiteSpace(t.Merchant)?t.Description:t.Merchant,DateOnly.FromDateTime(t.TransactionDate),t.Amount,t.AccountName,t.Description)),today);
        var plan=await householdBudgets.GetMonthAsync(new(today.Year,today.Month,1));
        var targets=plan.Allocations.Where(a=>a.SourceKey.HasValue && a.IsScheduled && a.AllocationType is BudgetAllocationType.Bills or BudgetAllocationType.Housing or BudgetAllocationType.Subscriptions)
            .GroupBy(a=>a.SourceKey!.Value).Select(g=>new RecurringBudgetTarget(g.Key,g.Last().Name)).ToArray();
        var active=targets.Select(t=>t.Key).ToHashSet();
        var reviews=await context.RecurringExpenseReviews.AsNoTracking().Where(r=>r.HouseholdId==household).ToDictionaryAsync(r=>r.PatternKey);
        var paymentRows = await context.BillPayments.AsNoTracking().Include(p => p.Links).Where(p => p.HouseholdId == household && p.IsPaid).ToListAsync();
        var payments = paymentRows.SelectMany(p => p.Links.Select(l => new { TransactionId = (int?)l.TransactionId, p.AllocationKey })
            .Concat(p.TransactionId is null ? [] : new[] { new { p.TransactionId, p.AllocationKey } })).ToList();
        var suggestions=patterns.Select(p=> {
            reviews.TryGetValue(p.Key,out var review);
            var linked=payments.Where(b=>p.Transactions.Any(t=>t.Id==b.TransactionId) && active.Contains(b.AllocationKey)).Select(b=>(Guid?)b.AllocationKey).Distinct().ToArray();
            var match=review?.AllocationKey is Guid key && active.Contains(key) ? key : linked.Length==1 ? linked[0] : null;
            var dismissed=review?.Dismissed==true && Math.Abs(review.ReviewedAmount-p.TypicalAmount)<=Math.Max(2,review.ReviewedAmount*.2m);
            var suggested=targets.Where(t=>RecurringExpenseDetector.Normalize(t.Name)==RecurringExpenseDetector.Normalize(p.Merchant)).ToArray();
            return p with { Reviewed=review!=null,Status=match.HasValue?"Covered":dismissed?"Dismissed":"NeedsReview",AllocationKey=match,SuggestedAllocationKey=suggested.Length==1?suggested[0].Key:null };
        }).ToArray();
        return new(from,today,rows.Count,suggestions,targets);
    }
    public async Task ReviewAsync(ReviewRecurringRequest request)
    {
        if (request.Action is not ("Dismiss" or "Match" or "Reset")) throw new ArgumentException("Choose dismiss, match, or reset.");
        var scan=await ScanAsync(); var suggestion=scan.Suggestions.SingleOrDefault(s=>s.Key==request.Key) ?? throw new ArgumentException("This pattern is no longer available. Scan again.");
        if (request.Action=="Match" && !scan.BudgetItems.Any(t=>t.Key==request.AllocationKey)) throw new ArgumentException("Choose a current scheduled bill or subscription.");
        var household=await user.GetCurrentHouseholdIdAsync();
        var row=await context.RecurringExpenseReviews.SingleOrDefaultAsync(r=>r.HouseholdId==household && r.PatternKey==request.Key);
        if (request.Action=="Reset") { if(row!=null) context.Remove(row); }
        else {
            if(row==null) { row=new() { HouseholdId=household,PatternKey=request.Key }; context.Add(row); }
            row.Dismissed=request.Action=="Dismiss"; row.AllocationKey=request.Action=="Match"?request.AllocationKey:null; row.ReviewedAmount=suggestion.TypicalAmount;
        }
        try {
            if (request.Action is "Dismiss" or "Match") {
                var alerts=await context.HouseholdAlerts.Where(a=>a.HouseholdId==household && a.PatternKey==request.Key && a.Active).ToListAsync();
                foreach(var alert in alerts) alert.Active=false;
            }
            await context.SaveChangesAsync();
        }
        catch(DbUpdateException) { throw new BudgetConflictException("This suggestion was reviewed elsewhere. Scan again."); }
    }
}
