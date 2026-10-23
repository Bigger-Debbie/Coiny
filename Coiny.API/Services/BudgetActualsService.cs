using Coiny.API.Data;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class BudgetActualsService(CoinyContext context, ICurrentUserService user, IBudgetService budgets, TimeProvider clock)
{
    private static void ValidateMonth(DateOnly month)
    {
        if (month.Day != 1 || month == DateOnly.MinValue) throw new ArgumentException("Choose the first day of a budget month.");
    }
    private async Task<BudgetMonthResponse> Plan(DateOnly month)
    {
        ValidateMonth(month);
        var plan = await budgets.GetMonthAsync(month);
        if (plan.RevisionId is null) throw new ArgumentException("Publish a budget for this month before assigning transactions.");
        return plan;
    }
    private static BudgetMonthlyItem? Target(BudgetMonthResponse plan, Guid? key)
    {
        if (key is null) return null;
        return plan.Allocations.SingleOrDefault(a => a.SourceKey == key) ?? throw new ArgumentException("Choose an item in this month's published budget.");
    }
    public Task<BudgetActualsResponse> GetAsync(DateOnly month, string view = "unassigned", string? type = null, int page = 1, Guid? allocationKey = null) =>
        Report(month,view,type,page,allocationKey,null);
    public async Task<BudgetActualsResponse> GetPeriodAsync(DateOnly? asOf = null, string view = "unassigned", string? type = null, int page = 1, Guid? allocationKey = null)
    {
        var period = await budgets.GetIncomePeriodAsync(asOf);
        if (period.Plan is null || period.StartDate is null || period.EndDate is null) throw new ArgumentException("Choose a complete published income period.");
        return await Report(new(period.StartDate.Value.Year,period.StartDate.Value.Month,1),view,type,page,allocationKey,period);
    }
    private async Task<BudgetActualsResponse> Report(DateOnly month, string view, string? type, int page, Guid? allocationKey, BudgetIncomePeriodResponse? incomePeriod)
    {
        if (page < 1 || page > 100000 || view is not ("unassigned" or "all") || type is not (null or "Expense" or "Transfer"))
            throw new ArgumentException("Choose a valid view, transaction type, and page.");
        var household = await user.GetCurrentHouseholdIdAsync();
        var plan = incomePeriod?.Plan ?? await Plan(month);
        if (allocationKey.HasValue) Target(plan, allocationKey);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
        var rangeStart = incomePeriod?.StartDate ?? month;
        var last = incomePeriod?.EndDate ?? new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
        DateOnly? through = today < rangeStart ? null : today < last ? today : last;
        var rules = await context.BudgetCategoryRules.AsNoTracking().Where(r => r.HouseholdId == household && r.EffectiveFromMonth <= last).ToListAsync();
        var effective = rules.Where(r => r.EffectiveFromMonth <= month).GroupBy(r => r.CategoryId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.EffectiveFromMonth).First());
        var categories = await context.Categories.AsNoTracking().Where(c => c.HouseholdId == household && c.CategoryType == CategoryType.Expense).OrderBy(c => c.Name).ToListAsync();
        var defaults = categories.Select(c =>
        {
            effective.TryGetValue(c.Id, out var rule);
            return new BudgetCategoryDefault(c.Id, c.Name, c.IsActive, rule?.AllocationKey, rule?.EffectiveFromMonth, rule?.EffectiveFromMonth == month ? rule.EditVersion : 0);
        }).ToList();
        var ledger = new List<Transaction>();
        if (through.HasValue)
        {
            var start = rangeStart.ToDateTime(TimeOnly.MinValue); var end = through.Value.ToDateTime(TimeOnly.MaxValue);
            // A paired movement is counted once, on its receiving entry's posting date.
            ledger = await context.Transactions.AsNoTracking().Include(t => t.Category).Include(t => t.Account).Include(t => t.PairedTransaction)
                .Where(t => t.HouseholdId == household && t.TransactionDate >= start && t.TransactionDate <= end &&
                    (t.Category.CategoryType == CategoryType.Expense || t.Category.CategoryType == CategoryType.Transfer &&
                        (t.PairedTransactionId == null || t.TransferDirection == TransferDirection.In))).ToListAsync();
        }
        var ids = ledger.SelectMany(t => t.PairedTransactionId.HasValue ? new[] { t.Id, t.PairedTransactionId.Value } : new[] { t.Id }).ToArray();
        var payments = await context.BillPayments.AsNoTracking().Where(p => p.HouseholdId == household && p.TransactionId.HasValue && ids.Contains(p.TransactionId.Value)).ToListAsync();
        var overrides = await context.TransactionBudgetAssignments.AsNoTracking().Where(a => a.HouseholdId == household && ids.Contains(a.TransactionId)).ToListAsync();
        var rows = new List<BudgetActualsTransaction>();
        foreach (var t in ledger)
        {
            var payment = payments.SingleOrDefault(p => p.TransactionId == t.Id || t.PairedTransactionId.HasValue && p.TransactionId == t.PairedTransactionId);
            var assignment = overrides.SingleOrDefault(a => a.TransactionId == t.Id || a.TransactionId == t.PairedTransactionId);
            var source = payment is not null ? "Payment" : assignment is not null ? "Override" : "Unassigned";
            Guid? key = payment?.AllocationKey ?? assignment?.AllocationKey;
            var postingMonth = new DateOnly(t.TransactionDate.Year,t.TransactionDate.Month,1);
            var rule = rules.Where(r => r.CategoryId == t.CategoryId && r.EffectiveFromMonth <= postingMonth).OrderByDescending(r => r.EffectiveFromMonth).FirstOrDefault();
            if (payment is null && assignment is null && t.Category.CategoryType == CategoryType.Expense && rule is not null)
            { key = rule.AllocationKey; source = key.HasValue ? "Category" : "Unassigned"; }
            var target = plan.Allocations.SingleOrDefault(a => a.SourceKey == key);
            if (t.Category.CategoryType == CategoryType.Transfer && target?.AllocationType is not (BudgetAllocationType.Savings or BudgetAllocationType.Investments)) target = null;
            rows.Add(new(t.Id, t.EditVersion, t.PairedTransaction?.EditVersion, DateOnly.FromDateTime(t.TransactionDate), t.Description, t.Amount,
                t.Account.Name, t.Category.Name, t.Category.CategoryType, target?.SourceKey, target?.Name, source, t.Merchant));
        }
        var items = plan.Allocations.Select(a =>
        {
            var assigned = rows.Where(t => t.AllocationKey == a.SourceKey).ToList();
            var actual = assigned.Sum(t => t.Amount);
            var pending = plan.Events.Where(e => e.SourceKey == a.SourceKey && e.Payment is { IsPaid: true, TransactionId: null }).Sum(e => e.Amount);
            return new BudgetActualsItem(a.SourceKey!.Value, a.Name, a.AllocationType!.Value, a.PlannedAmount, actual, a.PlannedAmount - actual, pending, assigned.Count);
        }).ToList();
        var visible = rows.Where(t => (allocationKey.HasValue ? t.AllocationKey == allocationKey : view == "all" || t.AllocationKey is null) && (type is null || t.CategoryType.ToString() == type))
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).ToList();
        var totalPages = Math.Max(1, (visible.Count + 9) / 10); page = Math.Min(page, totalPages);
        bool Contribution(BudgetMonthlyItem item) => item.AllocationType is BudgetAllocationType.Savings or BudgetAllocationType.Investments;
        var contributionKeys = plan.Allocations.Where(Contribution).Select(a => a.SourceKey).ToHashSet();
        var plannedPoints = incomePeriod?.PlannedPoints ?? Enumerable.Range(1, last.Day).Select(day =>
        {
            var date = new DateOnly(month.Year, month.Month, day);
            decimal Planned(bool contribution)
            {
                var scheduled = plan.Events.Where(e => !e.IsIncome && e.Date <= date &&
                    (e.AllocationType is BudgetAllocationType.Savings or BudgetAllocationType.Investments) == contribution).Sum(e => e.Amount);
                var monthly = plan.Allocations.Where(a => a.OccurrenceCount == 0 && Contribution(a) == contribution).Sum(a => a.PlannedAmount);
                // Round the cumulative pace, not each daily increment, so month end equals the plan exactly.
                return scheduled + decimal.Round(monthly * day / last.Day, 2, MidpointRounding.AwayFromZero);
            }
            return new BudgetComparisonPoint(date, Planned(false), null, Planned(true), null);
        }).ToList();
        var points = plannedPoints.Select(p => {
            var recorded = rows.Where(t => t.Date <= p.Date).ToList();
            decimal? spending = through.HasValue && p.Date <= through.Value
                ? recorded.Where(t => t.CategoryType == CategoryType.Expense && !contributionKeys.Contains(t.AllocationKey)).Sum(t => t.Amount) : null;
            decimal? contributions = through.HasValue && p.Date <= through.Value
                ? recorded.Where(t => t.AllocationKey.HasValue && contributionKeys.Contains(t.AllocationKey)).Sum(t => t.Amount) : null;
            return new BudgetComparisonPoint(p.Date,p.PlannedSpending,spending,p.PlannedContributions,contributions);
        }).ToList();
        return new()
        {
            Month = month, StartDate = rangeStart, EndDate = last, ThroughDate = through, Planned = plan.Allocated, AssignedActual = items.Sum(a => a.Actual),
            UnassignedExpenses = rows.Where(t => t.AllocationKey is null && t.CategoryType == CategoryType.Expense).Sum(t => t.Amount),
            AwaitingTransaction = items.Sum(a => a.AwaitingTransaction), Items = items, CategoryDefaults = defaults,
            Transactions = visible.Skip((page - 1) * 10).Take(10).ToList(), Page = page, TotalCount = visible.Count, Points = points
        };
    }
    public async Task SaveCategoryAsync(int categoryId, SaveBudgetCategoryRuleRequest request)
    {
        if (request.Month is not { } month) throw new ArgumentException("Choose a budget month.");
        var household = await user.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var plan = await Plan(month); Target(plan, request.AllocationKey);
        if (!await context.Categories.AnyAsync(c => c.Id == categoryId && c.HouseholdId == household && c.CategoryType == CategoryType.Expense))
            throw new ArgumentException("Choose an expense category in your household.");
        var rule = await context.BudgetCategoryRules.SingleOrDefaultAsync(r => r.HouseholdId == household && r.CategoryId == categoryId && r.EffectiveFromMonth == month);
        if ((rule?.EditVersion ?? 0) != request.ExpectedEditVersion) throw new BudgetConflictException("This category default changed. Reload before saving.");
        if (rule is null) { rule = new() { HouseholdId = household, CategoryId = categoryId, EffectiveFromMonth = month }; context.BudgetCategoryRules.Add(rule); }
        rule.AllocationKey = request.AllocationKey; rule.EditVersion++;
        try { await context.SaveChangesAsync(); await unit.CommitAsync(); }
        catch (DbUpdateException) { throw new BudgetConflictException("This category default changed. Reload before saving."); }
    }
    public async Task SaveAssignmentAsync(int transactionId, SaveTransactionBudgetAssignmentRequest request)
    {
        if (request.Month is not { } month) throw new ArgumentException("Choose a budget month.");
        var household = await user.GetCurrentHouseholdIdAsync();
        await using var unit = await context.Database.BeginTransactionAsync();
        var plan = await Plan(month); var target = Target(plan, request.AllocationKey);
        if (request.UseCategoryDefault && request.AllocationKey.HasValue) throw new ArgumentException("Choose either a category default or an individual budget item.");
        var entry = await context.Transactions.Include(t => t.Category).SingleOrDefaultAsync(t => t.Id == transactionId && t.HouseholdId == household)
            ?? throw new ArgumentException("Transaction not found in your household.");
        var pair = entry.PairedTransactionId is int pairId ? await context.Transactions.SingleOrDefaultAsync(t => t.Id == pairId && t.HouseholdId == household) : null;
        if (entry.EditVersion != request.ExpectedEditVersion || pair?.EditVersion != request.ExpectedPairedEditVersion)
            throw new BudgetConflictException("The transaction changed. Reload before assigning it.");
        if (entry.Category.CategoryType == CategoryType.Income || entry.Category.CategoryType == CategoryType.Transfer && target is not null && target.AllocationType is not (BudgetAllocationType.Savings or BudgetAllocationType.Investments))
            throw new ArgumentException("Transfers can only be assigned to savings or investing; income is outside this allocation comparison.");
        var posting = DateOnly.FromDateTime(pair is not null && entry.TransferDirection == TransferDirection.Out ? pair.TransactionDate : entry.TransactionDate);
        if (posting.Year != month.Year || posting.Month != month.Month) throw new ArgumentException("Choose the transaction's posting month.");
        var ids = pair is null ? new[] { entry.Id } : new[] { entry.Id, pair.Id };
        if (await context.BillPayments.AnyAsync(p => p.TransactionId.HasValue && ids.Contains(p.TransactionId.Value)))
            throw new BudgetConflictException("This movement is assigned by a linked payment. Update that link from the budget calendar.");
        var assignment = await context.TransactionBudgetAssignments.SingleOrDefaultAsync(a => ids.Contains(a.TransactionId));
        if (request.UseCategoryDefault) { if (assignment is not null) context.TransactionBudgetAssignments.Remove(assignment); }
        else
        {
            if (assignment is null) { assignment = new() { HouseholdId = household, TransactionId = entry.Id }; context.TransactionBudgetAssignments.Add(assignment); }
            assignment.AllocationKey = request.AllocationKey;
        }
        entry.EditVersion++; if (pair is not null) pair.EditVersion++;
        try { await context.SaveChangesAsync(); await unit.CommitAsync(); }
        catch (DbUpdateException) { throw new BudgetConflictException("This movement changed. Reload before assigning it."); }
    }
}
