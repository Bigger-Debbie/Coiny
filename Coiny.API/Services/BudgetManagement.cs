using System.Text.Json;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public partial class BudgetService
{
    private static DateOnly Effective(BudgetRevision revision) => revision.EffectiveFromDate ?? revision.EffectiveFromMonth;
    private static BudgetRevision? Applicable(IReadOnlyList<BudgetRevision> revisions, DateOnly date) => revisions
        .Where(r => Effective(r) <= date).OrderByDescending(Effective).ThenByDescending(r => r.RevisionNumber).FirstOrDefault();
    private List<BillPayment> skippedSavings = [];
    private Dictionary<(Guid Fund,Guid Component),decimal> fundedBalances = [];
    private List<SavingsActivity> fundContributions = [];
    private Dictionary<Guid,int> fundIds = [];
    private DateOnly fundingPeriodEnd = DateOnly.MaxValue;
    private DateOnly fundingPeriodStart = DateOnly.MinValue;
    private async Task<List<BudgetRevision>> Published(int household)
    {
        skippedSavings=await context.BillPayments.AsNoTracking().Where(p=>p.HouseholdId==household && p.IsSkipped).ToListAsync();
        var funds=await context.Set<SavingsFund>().AsNoTracking().Include(f=>f.Activities).Where(f=>f.HouseholdId==household).ToListAsync();
        fundedBalances=funds.SelectMany(f=>JsonSerializer.Deserialize<Coiny.API.DTOs.Savings.SavingsPlan>(f.PlanJson)!.Components.Select(c=>new {Key=(f.SourceKey,c.Key),Balance=f.Activities.Where(a=>a.ComponentKey==c.Key).Sum(a=>a.Amount)})).ToDictionary(p=>p.Key,p=>p.Balance);
        fundIds=funds.ToDictionary(f=>f.SourceKey,f=>f.Id);
        fundContributions=funds.SelectMany(f=>f.Activities.Where(a=>a.Kind is "Contribution" or "ContributionLinked" or "CalendarContribution")).ToList();
        var revisions=await Revisions(household).Where(r => r.State == BudgetRevisionState.Published).ToListAsync();
        var today=Today();fundingPeriodStart=DateOnly.MinValue;
        if(funds.Count>0)
        {
            var paydays=new List<DateOnly>();
            for(var m=new DateOnly(today.Year,today.Month,1).AddMonths(-12);m<=today.AddMonths(12);m=m.AddMonths(1))
                foreach(var revision in revisions)
                    foreach(var income in revision.IncomeSources)
                        paydays.AddRange(schedules.GetOccurrences(income.Schedule,m).Where(d=>Applicable(revisions,d)?.Id==revision.Id));
            fundingPeriodStart=paydays.Where(d=>d<=today).DefaultIfEmpty(DateOnly.MinValue).Max();
            fundingPeriodEnd=paydays.Where(d=>d>today).Select(d=>d.AddDays(-1)).DefaultIfEmpty(DateOnly.MaxValue).Min();
        }
        return revisions;
    }
    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ApplicationTimeZone).DateTime);
    private sealed record ProtectedPayment(BudgetScheduledEvent Event, DateOnly? SuppressedDate, Dictionary<DateOnly, decimal>? DailyAmounts = null);
    private static List<ProtectedPayment> Protected(BudgetRevision revision) => string.IsNullOrWhiteSpace(revision.ProtectedPaymentsJson) ? [] : JsonSerializer.Deserialize<List<ProtectedPayment>>(revision.ProtectedPaymentsJson) ?? [];

    public async Task<BudgetManagementResponse> GetManagementAsync()
    {
        var revisions = await Published(await currentUser.GetCurrentHouseholdIdAsync());
        var latest = revisions.OrderByDescending(Effective).ThenByDescending(r => r.RevisionNumber).FirstOrDefault()
            ?? throw new ArgumentException("Publish your first budget before managing its items.");
        return new(Response(latest), Effective(latest) > Today() ? Effective(latest) : Today());
    }

    public async Task<BudgetItemReview> ChangeItemAsync(BudgetItemChange request, bool preview)
    {
        var household = await currentUser.GetCurrentHouseholdIdAsync();
        await using var transaction = context.Database.CurrentTransaction is null ? await context.Database.BeginTransactionAsync() : null;
        var published = await Published(household);
        var baseline = published.OrderByDescending(Effective).ThenByDescending(r => r.RevisionNumber).FirstOrDefault()
            ?? throw new ArgumentException("Publish your first budget before managing its items.");
        if (baseline.Id != request.ExpectedRevisionId) throw new BudgetConflictException("The household budget changed. Reload this item before saving.");
        if (request.EffectiveDate < Today() || request.EffectiveDate < Effective(baseline) || request.EffectiveDate == DateOnly.MaxValue)
            throw new ArgumentException("Choose today or a later date, on or after the latest scheduled budget update.");
        if (await Revisions(household).AnyAsync(r => r.State == BudgetRevisionState.Draft))
            throw new BudgetConflictException("A saved household draft exists. Finish publishing it before editing published items.");
        var incomes = baseline.IncomeSources.Select(Income).ToList(); var allocations = baseline.Allocations.Select(Allocation).ToList();
        var key = request.SourceKey ?? request.NewSourceKey ?? Guid.NewGuid();
        if (!request.FundManaged && await context.Set<SavingsFund>().AnyAsync(f => f.HouseholdId == household && f.SourceKey == key))
            throw new ArgumentException("Manage this savings goal from Savings funds so its account and components stay in sync.");
        if (key == Guid.Empty || request.Remove && request.SourceKey is null) throw new ArgumentException("Choose an existing budget item to remove.");
        if (request.SourceKey.HasValue && !(request.IsIncome ? incomes.Any(i => i.SourceKey == key) : allocations.Any(a => a.SourceKey == key)))
            throw new ArgumentException("This item is not in the household's latest published budget.");
        if (request.IsIncome)
        {
            incomes.RemoveAll(i => i.SourceKey == key);
            if (!request.Remove) { var item = request.Income ?? throw new ArgumentException("Income details are required."); item.SourceKey = key; incomes.Add(item); }
        }
        else
        {
            allocations.RemoveAll(i => i.SourceKey == key);
            if (!request.Remove) { var item = request.Allocation ?? throw new ArgumentException("Allocation details are required."); item.SourceKey = key; allocations.Add(item); }
        }
        var month = new DateOnly(request.EffectiveDate.Year, request.EffectiveDate.Month, 1);
        ValidateDefinitions(new() { EffectiveFromMonth = month, SetupStep = BudgetSetupStep.Review, IncomeSources = incomes, Allocations = allocations });
        if (incomes.Count == 0 || incomes.Any(i => i.Amount <= 0)) throw new ArgumentException("Keep at least one income source, with positive take-home amounts.");
        if (allocations.Any(a => a.AllocationType is BudgetAllocationType.Housing or BudgetAllocationType.Bills or BudgetAllocationType.Subscriptions && a.Schedule is null))
            throw new ArgumentException("Housing, bills, and subscriptions require a payment schedule.");
        var next = new BudgetRevision {
            Id = preview ? -1 : 0, HouseholdBudgetId = baseline.HouseholdBudgetId,
            RevisionNumber = (await context.BudgetRevisions.Where(r => r.HouseholdBudgetId == baseline.HouseholdBudgetId).MaxAsync(r => r.RevisionNumber)) + 1,
            State = BudgetRevisionState.Published, SetupStep = BudgetSetupStep.Review, PublishedAt = clock.GetUtcNow().UtcDateTime,
            EffectiveFromMonth = month, EffectiveFromDate = request.EffectiveDate,
            IncomeSources = incomes.Select(i => new BudgetIncomeSource { SourceKey = i.SourceKey!.Value, Name = i.Name.Trim(), Amount = i.Amount,
                Schedule = Schedule(i.Schedule), HealthInsurancePerPaycheck = i.HealthInsurancePerPaycheck, RetirementContributionPerPaycheck = i.RetirementContributionPerPaycheck }).ToList(),
            Allocations = allocations.Select(a => new BudgetAllocation { SourceKey = a.SourceKey!.Value, Name = a.Name.Trim(), Amount = a.Amount, AllocationType = a.AllocationType,
                SavingsPlanJson = a.SavingsPlan is null ? null : JsonSerializer.Serialize(a.SavingsPlan), Schedule = a.Schedule is null ? null : Schedule(a.Schedule) }).ToList(),
        };
        // Capture paid occurrences before changing the definition. Include already paid future dates,
        // even if an item is removed or its schedule changes. No ledger or payment rows are changed.
        var locks = new List<ProtectedPayment>();
        var payments = await context.BillPayments.AsNoTracking().Where(p => p.HouseholdId == household && p.IsPaid && p.DueDate >= request.EffectiveDate).ToListAsync();
        foreach (var payment in payments)
        {
            var old = await GetMonthAsync(new(payment.DueDate.Year, payment.DueDate.Month, 1));
            var occurrence = old.Events.FirstOrDefault(e => !e.IsIncome && e.SourceKey == payment.AllocationKey && e.Date == payment.DueDate);
            occurrence ??= (await GetIncomePeriodAsync(payment.DueDate)).Contributions.FirstOrDefault(e => e.SourceKey == payment.AllocationKey && e.Date == payment.DueDate);
            if (occurrence is null) continue;
            DateOnly? suppressed = null;
            if (!occurrence.IsPeriodSetAside)
            {
                var dates = old.Events.Where(e => !e.IsIncome && e.SourceKey == payment.AllocationKey).OrderBy(e => e.Date).ToList();
                var replacement = ProjectMonth(next, old.Month, false).Events.Where(e => !e.IsIncome && e.SourceKey == payment.AllocationKey).OrderBy(e => e.Date).ElementAtOrDefault(dates.FindIndex(e => e.Date == payment.DueDate));
                suppressed = replacement?.Date;
            }
            Dictionary<DateOnly, decimal>? daily = null;
            if (occurrence.IsPeriodSetAside)
            {
                var period = await GetIncomePeriodAsync(payment.DueDate);
                daily = PeriodMonths(period.StartDate!.Value, period.EndDate!.Value, published)
                    .SelectMany(p => Enumerable.Range(p.From.DayNumber, p.To.DayNumber-p.From.DayNumber+1).Select(day => {
                        var date = DateOnly.FromDayNumber(day);
                        var item = p.Plan.Allocations.SingleOrDefault(a => a.SourceKey == payment.AllocationKey);
                        return new { Date = date, Amount = item is null ? 0 : AmountBetween(item,date.Day,date.Day,date.Year,date.Month) };
                    })).ToDictionary(p => p.Date,p => p.Amount);
            }
            locks.Add(new(occurrence, suppressed, daily));
        }
        next.ProtectedPaymentsJson = JsonSerializer.Serialize(locks);
        var before = await GetMonthAsync(month);
        var beforePeriod = await GetPeriod(request.EffectiveDate, null, published);
        if (!preview)
        {
            // Reserve the baseline's edit version atomically so concurrent writers cannot publish
            // two changes from the same reviewed plan (including a racing preview/save).
            var updated = await context.BudgetRevisions.Where(r => r.Id == baseline.Id && r.EditVersion == baseline.EditVersion)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.EditVersion, r => r.EditVersion + 1));
            if (updated != 1) throw new BudgetConflictException("The household budget changed. Reload before saving.");
            context.BudgetRevisions.Add(next);
            try { await context.SaveChangesAsync(); }
            catch (DbUpdateException) { throw new BudgetConflictException("The household budget changed. Reload before saving."); }
        }
        var after = ProjectPublishedMonth(published.Append(next).ToList(), month);
        await ApplyPayments(household, after.Events);
        if (!preview && transaction is not null) await transaction.CommitAsync();
        return new(before, after, locks.Count, request.EffectiveDate, beforePeriod, await GetPeriod(request.EffectiveDate, null, published.Append(next).ToList()));
    }

    private BudgetMonthResponse ProjectPublishedMonth(IReadOnlyList<BudgetRevision> published, DateOnly month, bool includePoints = true)
    {
        var last = new DateOnly(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
        if (Applicable(published, last) is null) return new() { Month = month };
        var plan = BuildPeriodPlan(month, last, published, []);
        if (includePoints) plan.PlannedPoints = PeriodPoints(month, last, plan, published, true);
        return plan;
    }
}
