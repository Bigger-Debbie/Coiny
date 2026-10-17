using Coiny.API.Data;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Services;

public class BudgetConflictException(string message) : InvalidOperationException(message);
public class BudgetDraftNotFoundException() : InvalidOperationException("Draft not found in the current household, or already published.");

public class BudgetService(
    CoinyContext context,
    ICurrentUserService currentUser,
    IBudgetScheduleService schedules,
    TimeProvider clock) : IBudgetService
{
    private static readonly TimeZoneInfo ApplicationTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public async Task<BudgetSetupStatusResponse> GetSetupStatusAsync()
    {
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        var month = CurrentMonth();
        var firstMonth = await Revisions(householdId).Where(r => r.State == BudgetRevisionState.Published)
            .Select(r => (DateOnly?)r.EffectiveFromMonth).MinAsync();
        var draft = await Revisions(householdId).SingleOrDefaultAsync(r => r.State == BudgetRevisionState.Draft);
        return new() {
            CurrentMonth = month, HasPublishedBudget = firstMonth.HasValue, FirstPublishedMonth = firstMonth,
            CurrentRevisionId = (await ApplicableRevision(householdId, month))?.Id,
            Draft = draft is null ? null : Response(draft),
        };
    }

    public async Task<BudgetDraftPreviewResponse> PreviewDraftAsync(int draftId, int expectedEditVersion, DateOnly month)
    {
        ValidateMonth(month);
        var draft = await OwnedDraft(await currentUser.GetCurrentHouseholdIdAsync(), draftId);
        CheckVersion(draft, expectedEditVersion);
        if (month < draft.EffectiveFromMonth)
            throw new ArgumentException("Preview month must be on or after the draft's effective month.");
        return new() { DraftId = draft.Id, EditVersion = draft.EditVersion, Month = ProjectMonth(draft, month) };
    }

    public async Task<BudgetRevisionResponse?> GetDraftAsync()
    {
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        var draft = await Revisions(householdId).SingleOrDefaultAsync(r => r.State == BudgetRevisionState.Draft);
        return draft is null ? null : Response(draft);
    }

    public async Task<BudgetRevisionResponse> StartDraftAsync(DateOnly? effectiveFromMonth = null)
    {
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var existing = await Revisions(householdId).SingleOrDefaultAsync(r => r.State == BudgetRevisionState.Draft);
        if (existing is not null)
        {
            await transaction.CommitAsync();
            return Response(existing);
        }

        var month = effectiveFromMonth ?? CurrentMonth();
        ValidateMonth(month);
        if (month < CurrentMonth())
            throw new ArgumentException("New revisions must start in the current month or later.");
        var budget = await context.HouseholdBudgets.AsNoTracking().SingleOrDefaultAsync(b => b.HouseholdId == householdId);
        if (budget is null)
        {
            budget = new HouseholdBudget { HouseholdId = householdId };
            context.HouseholdBudgets.Add(budget);
            await context.SaveChangesAsync();
        }

        var baseline = await ApplicableRevision(householdId, month);
        var number = await context.BudgetRevisions.Where(r => r.HouseholdBudgetId == budget.Id)
            .Select(r => (int?)r.RevisionNumber).MaxAsync() ?? 0;
        var draft = new BudgetRevision {
            HouseholdBudgetId = budget.Id, RevisionNumber = checked(number + 1),
            EffectiveFromMonth = month, SetupStep = baseline is null ? BudgetSetupStep.Income : BudgetSetupStep.Review,
        };
        if (baseline is not null)
        {
            foreach (var source in baseline.IncomeSources)
                draft.IncomeSources.Add(new() { SourceKey = source.SourceKey, Name = source.Name, Amount = source.Amount, HealthInsurancePerPaycheck = source.HealthInsurancePerPaycheck,
                    RetirementContributionPerPaycheck = source.RetirementContributionPerPaycheck, Schedule = Copy(source.Schedule) });
            foreach (var allocation in baseline.Allocations)
                draft.Allocations.Add(new() { Name = allocation.Name, Amount = allocation.Amount,
                    AllocationType = allocation.AllocationType, Schedule = allocation.Schedule is null ? null : Copy(allocation.Schedule) });
        }
        context.BudgetRevisions.Add(draft);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Response(draft);
    }

    public async Task<BudgetRevisionResponse> SaveDraftAsync(int draftId, BudgetDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        var draft = await OwnedDraft(householdId, draftId);
        CheckVersion(draft, request.ExpectedEditVersion);
        ValidateDefinitions(request);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var updated = await context.BudgetRevisions.Where(r => r.Id == draftId &&
                r.State == BudgetRevisionState.Draft && r.EditVersion == request.ExpectedEditVersion &&
                r.HouseholdBudget.HouseholdId == householdId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.EditVersion, r => r.EditVersion + 1)
                .SetProperty(r => r.EffectiveFromMonth, request.EffectiveFromMonth)
                .SetProperty(r => r.SetupStep, request.SetupStep));
        if (updated != 1) throw Conflict();

        await context.BudgetIncomeSources.Where(i => i.BudgetRevisionId == draftId).ExecuteDeleteAsync();
        await context.BudgetAllocations.Where(a => a.BudgetRevisionId == draftId).ExecuteDeleteAsync();
        draft.IncomeSources = request.IncomeSources.Select(i => new BudgetIncomeSource {
            BudgetRevisionId = draftId, SourceKey = i.SourceKey ?? Guid.NewGuid(), Name = i.Name.Trim(), Amount = i.Amount,
            HealthInsurancePerPaycheck = i.HealthInsurancePerPaycheck, RetirementContributionPerPaycheck = i.RetirementContributionPerPaycheck,
            Schedule = Schedule(i.Schedule),
        }).ToList();
        draft.Allocations = request.Allocations.Select(a => new BudgetAllocation {
            BudgetRevisionId = draftId, Name = a.Name.Trim(), Amount = a.Amount, AllocationType = a.AllocationType,
            Schedule = a.Schedule is null ? null : Schedule(a.Schedule),
        }).ToList();
        context.BudgetIncomeSources.AddRange(draft.IncomeSources);
        context.BudgetAllocations.AddRange(draft.Allocations);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        draft.EditVersion++;
        draft.SetupStep = request.SetupStep;
        draft.EffectiveFromMonth = request.EffectiveFromMonth;
        return Response(draft);
    }

    public async Task<BudgetRevisionResponse> PublishDraftAsync(int draftId, int expectedEditVersion)
    {
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        var draft = await OwnedDraft(householdId, draftId);
        CheckVersion(draft, expectedEditVersion);
        ValidateDefinitions(new() {
            EffectiveFromMonth = draft.EffectiveFromMonth, SetupStep = draft.SetupStep,
            IncomeSources = draft.IncomeSources.Select(Income).ToList(),
            Allocations = draft.Allocations.Select(Allocation).ToList(),
        });
        if (draft.EffectiveFromMonth < CurrentMonth())
            throw new ArgumentException("Choose the current month or a future month before publishing.");
        if (draft.SetupStep != BudgetSetupStep.Review)
            throw new ArgumentException("Review the household budget before publishing.");
        if (draft.IncomeSources.Count == 0 || draft.IncomeSources.Any(i => i.Amount <= 0))
            throw new ArgumentException("At least one income source is required; each must have a positive take-home amount.");
        if (draft.Allocations.Any(a => a.AllocationType is BudgetAllocationType.Housing or BudgetAllocationType.Bills && a.Schedule is null))
            throw new ArgumentException("Housing and bill allocations need a schedule before publishing.");

        var publishedAt = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await context.Database.BeginTransactionAsync();
        var updated = await context.BudgetRevisions.Where(r => r.Id == draftId &&
                r.State == BudgetRevisionState.Draft && r.EditVersion == expectedEditVersion &&
                r.HouseholdBudget.HouseholdId == householdId)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.State, BudgetRevisionState.Published)
                .SetProperty(r => r.PublishedAt, publishedAt).SetProperty(r => r.EditVersion, r => r.EditVersion + 1));
        if (updated != 1) throw Conflict();
        await transaction.CommitAsync();
        draft.EditVersion++;
        draft.State = BudgetRevisionState.Published;
        draft.PublishedAt = publishedAt;
        return Response(draft);
    }

    public async Task<BudgetMonthResponse> GetMonthAsync(DateOnly month)
    {
        ValidateMonth(month);
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        var revision = await ApplicableRevision(householdId, month);
        if (revision is null) return new() { Month = month };
        return ProjectMonth(revision, month);
    }

    private BudgetMonthResponse ProjectMonth(BudgetRevision revision, DateOnly month)
    {
        var income = new List<BudgetMonthlyItem>();
        var allocations = new List<BudgetMonthlyItem>();
        var events = new List<BudgetScheduledEvent>();
        foreach (var source in revision.IncomeSources.OrderBy(i => i.Id))
        {
            var dates = schedules.GetOccurrences(source.Schedule, month);
            income.Add(new() { SourceKey = source.SourceKey, Id = source.Id, Name = source.Name, OccurrenceCount = dates.Count, PlannedAmount = source.Amount * dates.Count,
                HealthInsurance = (source.HealthInsurancePerPaycheck ?? 0) * dates.Count,
                RetirementContributions = (source.RetirementContributionPerPaycheck ?? 0) * dates.Count });
            foreach (var date in dates)
                events.Add(new() { SourceKey = source.SourceKey, Date = date, ItemId = source.Id, Name = source.Name, IsIncome = true, Amount = source.Amount });
        }
        foreach (var allocation in revision.Allocations.OrderBy(a => a.Id))
        {
            var dates = allocation.Schedule is null ? Array.Empty<DateOnly>() : schedules.GetOccurrences(allocation.Schedule, month);
            allocations.Add(new() { Id = allocation.Id, Name = allocation.Name, AllocationType = allocation.AllocationType,
                OccurrenceCount = dates.Count, PlannedAmount = allocation.Amount * (allocation.Schedule is null ? 1 : dates.Count) });
            foreach (var date in dates)
                events.Add(new() { Date = date, ItemId = allocation.Id, Name = allocation.Name,
                    AllocationType = allocation.AllocationType, Amount = allocation.Amount });
        }
        var totalIncome = income.Sum(i => i.PlannedAmount);
        var allocated = allocations.Sum(a => a.PlannedAmount);
        return new() {
            Month = month, RevisionId = revision.Id, ExpectedIncome = totalIncome, Allocated = allocated,
            Unassigned = Math.Max(0, totalIncome - allocated), OverBudget = Math.Max(0, allocated - totalIncome),
            IncomeSources = income, Allocations = allocations,
            HealthInsurance = income.Sum(i => i.HealthInsurance), RetirementContributions = income.Sum(i => i.RetirementContributions),
            Events = events.OrderBy(e => e.Date).ThenBy(e => e.IsIncome ? 0 : 1).ThenBy(e => e.Name).ThenBy(e => e.ItemId).ToList(),
        };
    }

    public async Task<BudgetIncomePeriodResponse> GetIncomePeriodAsync(DateOnly? asOf = null, Guid? sourceKey = null)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ApplicationTimeZone).DateTime);
        var date = asOf ?? today;
        if (date == DateOnly.MinValue) throw new ArgumentException("Choose a valid reference date.");
        var householdId = await currentUser.GetCurrentHouseholdIdAsync();
        // Read one consistent set of published versions; drafts never enter period calculations.
        var published = await Revisions(householdId).Where(r => r.State == BudgetRevisionState.Published).ToListAsync();
        BudgetRevision? Revision(DateOnly month) => published.Where(r => r.EffectiveFromMonth <= month)
            .OrderByDescending(r => r.EffectiveFromMonth).ThenByDescending(r => r.RevisionNumber).FirstOrDefault();
        var current = Revision(new(date.Year, date.Month, 1));
        var sources = current?.IncomeSources.OrderBy(i => i.Id).Select(i => new BudgetIncomeSourceOption {
            SourceKey = i.SourceKey, Name = i.Name,
        }).ToList() ?? [];
        if (sourceKey.HasValue && !sources.Any(i => i.SourceKey == sourceKey))
            throw new ArgumentException("Choose an income source from this household's published plan for the reference date.");
        var events = new List<BudgetScheduledEvent>();
        var index = (date.Year - 1) * 12 + date.Month - 1;
        // A full year on each side covers every supported frequency, including yearly schedules.
        for (var m = Math.Max(0, index - 12); m <= Math.Min(9999 * 12 - 1, index + 12); m++)
        {
            var month = new DateOnly(m / 12 + 1, m % 12 + 1, 1);
            var revision = Revision(month);
            if (revision is not null) events.AddRange(ProjectMonth(revision, month).Events);
        }
        var paydays = events.Where(e => e.IsIncome && (!sourceKey.HasValue || e.SourceKey == sourceKey))
            .Select(e => e.Date).Distinct().Order().ToList();
        var start = paydays.Where(d => d <= date).Select(d => (DateOnly?)d).LastOrDefault();
        var next = paydays.Where(d => d > date).Select(d => (DateOnly?)d).FirstOrDefault();
        var end = start.HasValue && next.HasValue ? next.Value.AddDays(-1) : (DateOnly?)null;
        var periodEvents = start.HasValue && end.HasValue
            ? events.Where(e => !e.IsIncome && e.Date >= start && e.Date <= end)
                .OrderBy(e => e.Date).ThenBy(e => e.Name).ThenBy(e => e.ItemId).ToList() : [];
        var bills = periodEvents.Where(e => e.AllocationType is BudgetAllocationType.Housing or BudgetAllocationType.Bills).ToList();
        var contributions = periodEvents.Where(e => e.AllocationType is BudgetAllocationType.Savings or BudgetAllocationType.Investments).ToList();
        return new() {
            AsOf = date, SourceKey = sourceKey, Sources = sources, StartDate = start, EndDate = end, NextPayday = next,
            Bills = bills, BillsTotal = bills.Sum(e => e.Amount), Contributions = contributions,
            ContributionsTotal = contributions.Sum(e => e.Amount),
        };
    }

    private IQueryable<BudgetRevision> Revisions(int householdId) => context.BudgetRevisions.AsNoTracking()
        .Where(r => r.HouseholdBudget.HouseholdId == householdId)
        .Include(r => r.IncomeSources).Include(r => r.Allocations).AsSingleQuery();
    private Task<BudgetRevision?> ApplicableRevision(int householdId, DateOnly month) => Revisions(householdId)
        .Where(r => r.State == BudgetRevisionState.Published && r.EffectiveFromMonth <= month)
        .OrderByDescending(r => r.EffectiveFromMonth).ThenByDescending(r => r.RevisionNumber).FirstOrDefaultAsync();
    private async Task<BudgetRevision> OwnedDraft(int householdId, int id) =>
        await Revisions(householdId).SingleOrDefaultAsync(r => r.Id == id && r.State == BudgetRevisionState.Draft)
        ?? throw new BudgetDraftNotFoundException();
    private DateOnly CurrentMonth()
    {
        var today = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), ApplicationTimeZone);
        return new(today.Year, today.Month, 1);
    }
    private static void ValidateMonth(DateOnly month)
    {
        if (month == DateOnly.MinValue || month.Day != 1) throw new ArgumentException("Choose the first day of a valid calendar month.");
    }
    private static BudgetConflictException Conflict() => new("This draft changed. Reload the latest version before saving or publishing.");
    private static void CheckVersion(BudgetRevision draft, int expected)
    {
        if (expected < 0) throw new ArgumentException("Edit version cannot be negative.");
        if (draft.EditVersion != expected) throw Conflict();
    }
    private void ValidateDefinitions(BudgetDraftRequest request)
    {
        ValidateMonth(request.EffectiveFromMonth);
        if (!Enum.IsDefined(request.SetupStep)) throw new ArgumentException("Unsupported setup step.");
        if (request.IncomeSources is null || request.Allocations is null)
            throw new ArgumentException("Income sources and allocations are required; use empty lists for an unfinished draft.");
        var keys = request.IncomeSources.Where(i => i is not null && i.SourceKey.HasValue).Select(i => i.SourceKey!.Value).ToList();
        if (keys.Contains(Guid.Empty) || keys.Distinct().Count() != keys.Count)
            throw new ArgumentException("Income source keys must be nonempty and unique within a revision.");
        foreach (var source in request.IncomeSources)
        {
            if (source is null || source.Schedule is null) throw new ArgumentException("Each income source requires a schedule.");
            ValidateNameAndAmount(source.Name, source.Amount);
            if (source.HealthInsurancePerPaycheck.HasValue) ValidateNameAndAmount(source.Name, source.HealthInsurancePerPaycheck.Value);
            if (source.RetirementContributionPerPaycheck.HasValue) ValidateNameAndAmount(source.Name, source.RetirementContributionPerPaycheck.Value);
            schedules.Validate(Schedule(source.Schedule));
        }
        foreach (var allocation in request.Allocations)
        {
            if (allocation is null) throw new ArgumentException("An allocation cannot be empty.");
            ValidateNameAndAmount(allocation.Name, allocation.Amount);
            if (!Enum.IsDefined(allocation.AllocationType)) throw new ArgumentException("Unsupported allocation type.");
            if (allocation.Schedule is not null) schedules.Validate(Schedule(allocation.Schedule));
        }
    }
    private static void ValidateNameAndAmount(string name, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Names must contain 1–100 characters.");
        if (amount < 0 || amount > 9_999_999_999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Amounts must be nonnegative, within 18,2 precision, and use at most two decimal places.");
    }
    private static BudgetSchedule Schedule(BudgetScheduleInput s) => new() {
        Frequency = s.Frequency, StartDate = s.StartDate, EndDate = s.EndDate, DayOfMonth = s.DayOfMonth, SecondDayOfMonth = s.SecondDayOfMonth,
    };
    private static BudgetSchedule Copy(BudgetSchedule s) => Schedule(Input(s));
    private static BudgetScheduleInput Input(BudgetSchedule s) => new() {
        Frequency = s.Frequency, StartDate = s.StartDate, EndDate = s.EndDate, DayOfMonth = s.DayOfMonth, SecondDayOfMonth = s.SecondDayOfMonth,
    };
    private static BudgetIncomeInput Income(BudgetIncomeSource i) => new() { SourceKey = i.SourceKey, Name = i.Name, Amount = i.Amount, HealthInsurancePerPaycheck = i.HealthInsurancePerPaycheck,
        RetirementContributionPerPaycheck = i.RetirementContributionPerPaycheck, Schedule = Input(i.Schedule) };
    private static BudgetAllocationInput Allocation(BudgetAllocation a) => new() {
        Name = a.Name, Amount = a.Amount, AllocationType = a.AllocationType, Schedule = a.Schedule is null ? null : Input(a.Schedule),
    };
    private static BudgetRevisionResponse Response(BudgetRevision r) => new() {
        Id = r.Id, RevisionNumber = r.RevisionNumber, EditVersion = r.EditVersion, EffectiveFromMonth = r.EffectiveFromMonth,
        State = r.State, SetupStep = r.SetupStep, PublishedAt = r.PublishedAt,
        IncomeSources = r.IncomeSources.OrderBy(i => i.Id).Select(Income).ToList(),
        Allocations = r.Allocations.OrderBy(a => a.Id).Select(Allocation).ToList(),
    };
}
