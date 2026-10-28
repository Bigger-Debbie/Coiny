using System.Text.Json;
using Coiny.API.Data;
using Coiny.API.DTOs.Budgets;
using Coiny.API.DTOs.Savings;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Services;

public class SavingsFundsService(CoinyContext db, ICurrentUserService user, IBudgetService budgets, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
    private static SavingsPlan Plan(SavingsFund f) => JsonSerializer.Deserialize<SavingsPlan>(f.PlanJson)!;
    private IQueryable<SavingsFund> Funds(int household) => db.Set<SavingsFund>().Where(f => f.HouseholdId == household).Include(f => f.Activities).ThenInclude(a => a.Transaction);
    private async Task<SavingsFund> Owned(Guid key) => await Funds(await user.GetCurrentHouseholdIdAsync()).SingleOrDefaultAsync(f => f.SourceKey == key) ?? throw new ArgumentException("Savings fund not found in your household.");
    private static decimal Balance(SavingsFund f, Guid? component = null) => f.Activities.Where(a => component is null || a.ComponentKey == component).Sum(a => a.Amount);
    private static SavingsFundView View(SavingsFund f)
    {
        var plan = Plan(f);
        return new(f.Id,f.SourceKey,f.AccountId,f.Name,f.EditVersion,f.IsArchived,plan,Balance(f),
            plan.Components.Select(c => new SavingsComponentView(c,Balance(f,c.Key))).ToList(),
            f.Activities.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).Select(a => new SavingsActivityView(a.Id,a.ComponentKey,a.Date,a.Amount,a.Kind,a.Note,a.TransactionId,a.Transaction?.Merchant,a.Kind is "ContributionLinked" or "WithdrawalLinked" or "CalendarContribution" && a.TransactionId is null,a.OperationId)).ToList());
    }
    public async Task<IReadOnlyList<SavingsAccountView>> GetAsync(int? accountId = null)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        var accounts = await db.Accounts.AsNoTracking().Where(a => a.HouseholdId == household && a.AccountType == AccountType.Savings && (!accountId.HasValue || a.Id == accountId)).OrderBy(a => a.Name).ToListAsync();
        if (accountId.HasValue && accounts.Count == 0) throw new ArgumentException("Choose a savings account in your household.");
        var funds = await Funds(household).AsNoTracking().ToListAsync();
        var ids = accounts.Select(a => a.Id).ToArray();
        var cutoff = Today.ToDateTime(TimeOnly.MaxValue);
        var ledger = await db.Transactions.AsNoTracking().Include(t => t.Category).Where(t => t.HouseholdId == household && ids.Contains(t.AccountId) && t.TransactionDate <= cutoff).ToListAsync();
        var nextContributions=new List<BudgetScheduledEvent>();
        if(funds.Any(f=>!f.IsArchived))
        {
            var period=await budgets.GetIncomePeriodAsync(Today);
            nextContributions.AddRange(period.Contributions.Where(e=>e.Payment?.IsPaid!=true && e.Payment?.IsSkipped!=true));
            if(period.NextPayday.HasValue)nextContributions.AddRange((await budgets.GetIncomePeriodAsync(period.NextPayday)).Contributions.Where(e=>e.Payment?.IsPaid!=true && e.Payment?.IsSkipped!=true));
        }
        return accounts.Select(a => {
            var balance = a.OpeningBalance + ledger.Where(t => t.AccountId == a.Id).Sum(t => AccountBalanceCalculator.GetBalanceChange(a.AccountType,t.Category.CategoryType,t.Amount,t.TransferDirection));
            var held = funds.Where(f => f.AccountId == a.Id).Select(f=>{var view=View(f);var next=nextContributions.Where(e=>e.SourceKey==f.SourceKey).OrderBy(e=>e.Date).FirstOrDefault();return view with {NextContributionDate=next?.Date,NextContributionAmount=next?.Amount};}).ToList(); var assigned=held.Sum(f => f.Balance);
            return new SavingsAccountView(a.Id,a.Name,balance,assigned,Math.Max(0,balance-assigned),Math.Max(0,assigned-balance),held);
        }).ToList();
    }
    private async Task<(SavingsFund? Existing, Guid Key)> Validate(SaveSavingsFund r)
    {
        var household = await user.GetCurrentHouseholdIdAsync();
        if(r.OpeningAllocations is null)throw new ArgumentException("Opening allocations are required; use an empty list for an existing fund.");
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100) throw new ArgumentException("Give the fund a name of up to 100 characters.");
        if(r.Plan is null || r.Plan.Components is null || r.Plan.Components.Any(c=>c is null))throw new ArgumentException("A savings plan and components are required.");
        // Structural validation follows after authoritative existing allocations replace client balances.
        if (r.Plan.StartDate < Today) throw new ArgumentException("Start this contribution plan today or later. Existing balances can be assigned separately.");
        if (!await db.Accounts.AnyAsync(a => a.Id == r.AccountId && a.HouseholdId == household && a.AccountType == AccountType.Savings && a.IsActive && a.OpeningBalanceDate <= Today))
            throw new ArgumentException("Choose an active savings account that is already open.");
        var existing = r.SourceKey.HasValue ? await Funds(household).SingleOrDefaultAsync(f => f.SourceKey == r.SourceKey) : null;
        if ((existing?.EditVersion ?? 0) != r.ExpectedEditVersion) throw new BudgetConflictException("This fund changed. Reload it before saving.");
        if (existing is not null && (existing.IsArchived || existing.AccountId != r.AccountId)) throw new ArgumentException("Keep this fund in its original account.");
        if (existing is not null && r.OpeningAllocations.Count > 0) throw new ArgumentException("Use an allocation adjustment to move existing money.");
        foreach (var c in r.Plan.Components)
        {
            c.Name=c.Name.Trim();
            c.AlreadyFunded=existing is null ? r.OpeningAllocations.GetValueOrDefault(c.Key) : Balance(existing,c.Key);
            if (c.AlreadyFunded < 0) throw new ArgumentException("Resolve negative component balances before changing goals.");
            SavingsGoalCalculator.Money(c.AlreadyFunded);
        }
        SavingsGoalCalculator.Validate(r.Plan);
        if (existing is not null && existing.Activities.Any(a => !r.Plan.Components.Any(c => c.Key == a.ComponentKey)))
            throw new ArgumentException("Keep components with activity; their history and balances must be retained.");
        if (r.OpeningAllocations.Any(p => !r.Plan.Components.Any(c => c.Key == p.Key))) throw new ArgumentException("Choose a component in this fund.");
        foreach (var p in r.OpeningAllocations) SavingsGoalCalculator.Money(p.Value);
        var account = (await GetAsync(r.AccountId)).Single();
        if (r.OpeningAllocations.Values.Sum() > account.Unallocated) throw new ArgumentException("The opening allocations exceed the account's unallocated cash.");
        if (r.SourceKey == Guid.Empty) throw new ArgumentException("Choose a valid fund key.");
        return (existing,r.SourceKey ?? Guid.NewGuid());
    }
    private static BudgetAllocationInput Allocation(SaveSavingsFund r, Guid key) => new() { SourceKey=key,Name=r.Name.Trim(),Amount=SavingsGoalCalculator.MonthlyEquivalent(r.Plan),AllocationType=BudgetAllocationType.Savings,SavingsPlan=r.Plan };
    private async Task<BudgetItemChange> Change(SaveSavingsFund r, Guid key)
    {
        var management=await budgets.GetManagementAsync();
        if (r.ExpectedRevisionId != management.Revision.Id) throw new BudgetConflictException("The budget changed. Reload before reviewing the goal.");
        if (r.Plan.StartDate < management.EarliestEffectiveDate) throw new ArgumentException("Start after the latest scheduled budget change.");
        var item=management.Revision.Allocations.SingleOrDefault(a => a.SourceKey == key);
        if (item is not null && item.AllocationType != BudgetAllocationType.Savings) throw new ArgumentException("Only savings budget items can become funds.");
        return new() { ExpectedRevisionId=management.Revision.Id,EffectiveDate=r.Plan.StartDate,SourceKey=item?.SourceKey,NewSourceKey=key,FundManaged=true,Allocation=Allocation(r,key) };
    }
    public async Task<SavingsFundPreview> PreviewAsync(SaveSavingsFund r)
    {
        var (_,key)=await Validate(r);
        var household=await user.GetCurrentHouseholdIdAsync();
        var skipped=await db.BillPayments.AsNoTracking().Where(p=>p.HouseholdId==household && p.AllocationKey==key && p.IsSkipped).ToListAsync();
        var monthly=SavingsGoalCalculator.MonthlyEquivalent(r.Plan);
        var periods=new List<SavingsPeriodPreview>(); string? warning=null;
        var horizon=r.Plan.Components.Where(c=>c.TargetDate.HasValue).Select(c=>c.TargetDate!.Value).Append(r.Plan.StartDate.AddMonths(12).AddDays(-1)).Max();
        if (r.DraftId is int draftId)
        {
            var draft=await budgets.GetDraftAsync();
            if (draft?.Id != draftId || draft.EditVersion != r.DraftEditVersion) throw new BudgetConflictException("The budget draft changed. Save and reload it before reviewing savings.");
            var schedule=new BudgetScheduleService();
            var paydays=new List<DateOnly>();
            for(var m=DateOnly.FromDayNumber(Math.Max(draft.EffectiveFromMonth.DayNumber,new DateOnly(r.Plan.StartDate.Year,r.Plan.StartDate.Month,1).AddMonths(-12).DayNumber));m<=horizon.AddMonths(12);m=new DateOnly(m.Year,m.Month,1).AddMonths(1))
                foreach(var income in draft.IncomeSources) paydays.AddRange(schedule.GetOccurrences(new BudgetSchedule { Frequency=income.Schedule.Frequency,StartDate=income.Schedule.StartDate,EndDate=income.Schedule.EndDate,DayOfMonth=income.Schedule.DayOfMonth,SecondDayOfMonth=income.Schedule.SecondDayOfMonth },m));
            var dates=paydays.Distinct().Order().ToList();
            var previews=new Dictionary<DateOnly,BudgetMonthResponse>();
            for(var i=0;i+1<dates.Count;i++)
            {
                var start=dates[i];var end=dates[i+1].AddDays(-1); if(end<r.Plan.StartDate || start>horizon) continue;
                decimal available=0;
                for(var m=new DateOnly(start.Year,start.Month,1);m<=end;m=m.AddMonths(1))
                {
                    if(m<draft.EffectiveFromMonth) continue;
                    if(!previews.TryGetValue(m,out var plan)) { plan=(await budgets.PreviewDraftAsync(draftId,draft.EditVersion,m)).Month;previews[m]=plan; }
                    available+=plan.Events.Where(e=>e.Date>=start && e.Date<=end && e.IsIncome).Sum(e=>e.Amount);
                    available-=plan.Events.Where(e=>e.Date>=start && e.Date<=end && !e.IsIncome && e.SourceKey!=key).Sum(e=>e.Amount);
                    foreach(var a in plan.Allocations.Where(a=>!a.IsScheduled && a.SourceKey!=key))
                        available-=a.DailyAmounts?.Where(p=>p.Key>=start && p.Key<=end).Sum(p=>p.Value) ?? 0;
                }
                AddPeriod(start,end,available);
            }
        }
        else
        {
            var change=await Change(r,key);
            await budgets.ChangeItemAsync(change,true); // Same validation and protected-occurrence rules as saving.
            var actuals=new BudgetActualsService(db,user,budgets,clock);
            var cursor=r.Plan.StartDate;
            var forecastPeriods=new List<BudgetForecastPeriod>();var incomePeriods=new List<BudgetIncomePeriodResponse>();
            for(var year=0;year<11;year++)
            {
                var chunk=await budgets.GetIncomePeriodsAsync(cursor,12);
                incomePeriods.AddRange(chunk);forecastPeriods.AddRange((await actuals.GetForecastAsync(cursor,12)).Periods);
                if(chunk[^1].EndDate>=horizon)break;
                cursor=chunk[^1].NextPayday!.Value;
            }
            foreach(var period in incomePeriods)
            {
                var plan=period.Plan!;
                var summary=forecastPeriods.Single(p=>p.StartDate==period.StartDate);
                var existing=plan.Allocations.Where(a=>a.SourceKey==key).Sum(a=>a.PlannedAmount);
                if(period.StartDate<=Today && period.EndDate>=Today)
                    existing=(await actuals.GetPeriodAsync(period.StartDate)).Items.SingleOrDefault(i=>i.AllocationKey==key)?.ForecastRemaining??0;
                AddPeriod(period.StartDate!.Value,period.EndDate!.Value,summary.ForecastRemainder+existing);
            }
        }
        if(periods.Count==0) warning="Add a complete income schedule to see paycheck contributions and affordability.";
        var shortage=periods.Select(p=>Math.Max(0,-p.Remainder)).DefaultIfEmpty(0).Max();
        decimal supported=0;
        // Derive an affordable monthly rate over the actual calendar span, not an assumed pay frequency.
        if(periods.Count>0) supported=decimal.Round(Math.Max(0,periods.Sum(p=>p.AvailableBeforeGoal))*365.2425m/12/(periods[^1].EndDate.DayNumber-periods[0].StartDate.DayNumber+1),2);
        var targets=r.Plan.Components.Where(c=>c.Mode=="Target").ToList();
        DateOnly? completion=targets.Count>0 ? targets.Max(c=>c.TargetDate) : null;
        if(shortage>0) warning="Some pay periods cannot support this goal alongside the rest of your plan. Lower the target, extend the date, assign existing cash, or adjust other allocations.";
        var targetRemaining=targets.Sum(c=>Math.Max(0,c.Amount-c.AlreadyFunded));
        DateOnly? affordableCompletion=targetRemaining==0 && targets.Count>0 ? r.Plan.StartDate : null;
        decimal availableCarry=0;
        foreach(var period in periods)
        {
            if(targetRemaining<=0)break;
            var otherGoals=r.Plan.Components.Where(c=>c.Mode!="Target").Sum(c=>SavingsGoalCalculator.Between(r.Plan,c,period.StartDate,period.EndDate));
            availableCarry+=period.AvailableBeforeGoal-otherGoals;
            if(availableCarry>=targetRemaining){affordableCompletion=period.EndDate;break;}
        }
        if(skipped.Count>0)warning=(warning is null?"":warning+" ")+"Skipped periods are excluded. Review later funding or the target date if those contributions are still needed.";
        return new(monthly,periods,shortage,supported,completion,warning,affordableCompletion);
        void AddPeriod(DateOnly start,DateOnly end,decimal available)
        {
            var amounts=r.Plan.Components.ToDictionary(c=>c.Key,c=>SavingsGoalCalculator.Between(r.Plan,c,start,end));
            if(skipped.Any(p=>p.SkippedPeriodStart<=start && p.SkippedPeriodEnd>=end))foreach(var component in r.Plan.Components)amounts[component.Key]=0;
            var contribution=amounts.Values.Sum();
            periods.Add(new(start,end,contribution,available,available-contribution,amounts,SavingsGoalCalculator.ContributionDate(r.Plan,start,end)));
        }
    }
    public async Task<SavingsFundView> SaveAsync(SaveSavingsFund r)
    {
        await using var unit=await db.Database.BeginTransactionAsync();
        var review=await PreviewAsync(r);
        if(review.LargestShortfall>0 && !r.AcceptShortfall) throw new ArgumentException("Review the period shortfalls and explicitly accept a stretch goal, or adjust the contribution plan.");
        var (existing,key)=await Validate(r);
        if(r.DraftId is int draftId)
        {
            var draft=await budgets.GetDraftAsync();
            if(draft?.Id!=draftId || draft.EditVersion!=r.DraftEditVersion) throw new BudgetConflictException("The draft changed. Reload it before saving.");
            var allocations=draft.Allocations.Where(a=>a.SourceKey!=key).Append(Allocation(r,key)).ToList();
            await budgets.SaveDraftAsync(draftId,new() {FundManaged=true,ExpectedEditVersion=draft.EditVersion,EffectiveFromMonth=draft.EffectiveFromMonth,SetupStep=draft.SetupStep,IncomeSources=draft.IncomeSources.ToList(),Allocations=allocations});
        }
        else await budgets.ChangeItemAsync(await Change(r,key),false);
        var fund=existing ?? new SavingsFund {HouseholdId=await user.GetCurrentHouseholdIdAsync(),SourceKey=key,AccountId=r.AccountId,CreatedAt=clock.GetUtcNow().UtcDateTime};
        fund.Name=r.Name.Trim();fund.PlanJson=JsonSerializer.Serialize(r.Plan);fund.EditVersion++;
        if(existing is null)
        {
            db.Add(fund);var operation=Guid.NewGuid();
            foreach(var a in r.OpeningAllocations.Where(p=>p.Value>0)) fund.Activities.Add(new(){ComponentKey=a.Key,Date=Today,Amount=a.Value,Kind="Opening",OperationId=operation});
        }
        try {await db.SaveChangesAsync();await unit.CommitAsync();}
        catch(DbUpdateException){throw new BudgetConflictException("The fund or budget changed. Reload before saving.");}
        return View(fund);
    }
    public async Task<SavingsFundView> ActivityAsync(Guid key,SavingsActivityRequest r)
    {
        await using var unit=await db.Database.BeginTransactionAsync();
        var fund=await Owned(key);var plan=Plan(fund);
        if(fund.IsArchived)throw new ArgumentException("This fund is archived.");
        if(r.Amounts is null)throw new ArgumentException("Component amounts are required.");
        if(r.OperationId==Guid.Empty) throw new ArgumentException("An operation identifier is required.");
        if(r.Kind=="Contribution" && r.DueDate is null && r.Date>DateOnly.MinValue && r.Date<=Today)
            r.DueDate=(await budgets.GetIncomePeriodAsync(r.Date)).Contributions.FirstOrDefault(e=>e.SourceKey==key)?.Date;
        var old=fund.Activities.Where(a=>a.OperationId==r.OperationId).ToList();
        if(old.Count>0)
        {
            if(old.Count!=r.Amounts.Count || old.Any(a=>a.Note!=r.Note?.Trim() || a.DueDate!=r.DueDate || r.Amounts.GetValueOrDefault(a.ComponentKey)!=a.Amount || a.Date!=r.Date || a.TransactionId!=r.TransactionId || (a.Kind.EndsWith("Linked")?a.Kind[..^6]:a.Kind)!=r.Kind)) throw new BudgetConflictException("This operation identifier was already used. Reload the fund.");
            return View(fund);
        }
        if(fund.EditVersion!=r.ExpectedEditVersion) throw new BudgetConflictException("This fund changed. Reload it before recording activity.");
        if(r.Date>Today || r.Date<DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fund.CreatedAt,DateTimeKind.Utc),TimeZoneInfo.FindSystemTimeZoneById("America/New_York"))) || r.Kind is not ("Contribution" or "Withdrawal" or "Reallocate" or "Allocate") || r.Amounts.Count==0 || r.Note?.Length>500)
            throw new ArgumentException("Choose a valid activity type, date, and component amounts. Activity cannot predate fund setup or be in the future.");
        foreach(var a in r.Amounts)
        {
            if(!plan.Components.Any(c=>c.Key==a.Key)) throw new ArgumentException("Choose components in this fund.");
            SavingsGoalCalculator.Money(Math.Abs(a.Value));
            if(a.Value==0 || r.Kind is "Contribution" or "Allocate" && a.Value<0 || r.Kind=="Withdrawal" && a.Value>0 || Balance(fund,a.Key)+a.Value<0)
                throw new ArgumentException("Use positive contributions, negative withdrawals, and keep component balances nonnegative.");
        }
        var total=r.Amounts.Values.Sum();
        if(r.Kind=="Reallocate" && (total!=0 || r.TransactionId.HasValue)) throw new ArgumentException("A reallocation must balance to zero and does not create or link a bank transaction.");
        if(r.Kind=="Allocate" && (r.TransactionId.HasValue || r.DueDate.HasValue)) throw new ArgumentException("Assigning existing cash changes its purpose only; it is not a paycheck contribution or bank transaction.");
        Transaction? entry=null;
        if(r.TransactionId is int id)
        {
            entry=await db.Transactions.Include(t=>t.Category).Include(t=>t.PairedTransaction).SingleOrDefaultAsync(t=>t.Id==id && t.HouseholdId==fund.HouseholdId) ?? throw new ArgumentException("Choose a transaction in this household.");
            if(DateOnly.FromDateTime(entry.TransactionDate)!=r.Date || entry.TransactionDate>Today.ToDateTime(TimeOnly.MaxValue)) throw new ArgumentException("Use the transaction's posting date.");
            var linked=await db.Set<SavingsActivity>().Where(a=>!a.Kind.StartsWith("Voided:") && (a.TransactionId==id || entry.PairedTransactionId.HasValue && a.TransactionId==entry.PairedTransactionId)).ToListAsync();
            if(linked.Any(a=>a.SavingsFundId!=fund.Id)) throw new ArgumentException("This movement already belongs to another fund.");
            if(r.Kind=="Contribution" && !(entry.AccountId==fund.AccountId && (entry.Category.CategoryType==CategoryType.Income || entry.Category.CategoryType==CategoryType.Transfer && entry.TransferDirection==TransferDirection.In)))
                throw new ArgumentException("Link the incoming side of a deposit or transfer into this savings account.");
            if(r.Kind=="Contribution" && await db.BillPayments.AnyAsync(p=>p.HouseholdId==fund.HouseholdId && (p.TransactionId==id || entry.PairedTransactionId.HasValue && p.TransactionId==entry.PairedTransactionId || p.Links.Any(l=>l.TransactionId==id || l.TransactionId==entry.PairedTransactionId))))
                throw new ArgumentException("This movement is already linked in the calendar. Manage that contribution from its existing payment.");
            if(r.Kind=="Contribution" && await db.TransactionBudgetAssignments.AnyAsync(a=>a.TransactionId==id || a.TransactionId==entry.PairedTransactionId)) throw new ArgumentException("Remove this movement's individual budget assignment before recording it as a fund contribution.");
            if(r.Kind=="Withdrawal" && entry.Category.CategoryType!=CategoryType.Expense) throw new ArgumentException("Link the expense paid from the fund. A transfer back to spending is not a second withdrawal.");
            if(linked.Sum(a=>Math.Abs(a.Amount))+Math.Abs(total)>entry.Amount) throw new ArgumentException("The allocated amount exceeds this transaction's remaining amount.");
        }
        var account=(await GetAsync(fund.AccountId)).Single();
        if(total>account.Unallocated || total>0 && account.Shortfall>0) throw new ArgumentException("There is not enough unallocated cash in this savings account. Record the incoming transfer first or reallocate existing funds.");
        if(r.DueDate is DateOnly due && r.Kind=="Contribution")
        {
            var period=await budgets.GetIncomePeriodAsync(due);
            if(!period.Contributions.Any(e=>e.SourceKey==key && e.Date==due)) throw new ArgumentException("Choose this fund's scheduled contribution date.");
            var payment=await db.BillPayments.Include(p=>p.Links).SingleOrDefaultAsync(p=>p.HouseholdId==fund.HouseholdId && p.AllocationKey==key && p.DueDate==due);
            if(payment?.IsSkipped==true)throw new ArgumentException("Restore this skipped contribution before recording paycheck funding. Assign existing cash separately if it came from another source.");
            if(payment is not null && (payment.TransactionId.HasValue || payment.Links.Count>0)) throw new ArgumentException("Update the existing calendar payment link before recording this contribution separately.");
            if(payment is null){payment=new(){HouseholdId=fund.HouseholdId,AllocationKey=key,DueDate=due};db.BillPayments.Add(payment);}
            payment.IsPaid=fund.Activities.Where(a=>a.DueDate==due && a.Kind is "Contribution" or "ContributionLinked").Sum(a=>a.Amount)+total>=period.Contributions.Single(e=>e.SourceKey==key && e.Date==due).Amount;payment.PaidOn=r.Date;
        }
        foreach(var a in r.Amounts) fund.Activities.Add(new(){ComponentKey=a.Key,Amount=a.Value,Date=r.Date,Kind=r.Kind+(r.TransactionId.HasValue?"Linked":""),Note=r.Note?.Trim(),TransactionId=r.TransactionId,DueDate=r.DueDate,OperationId=r.OperationId});
        fund.EditVersion++;
        try {await db.SaveChangesAsync();await unit.CommitAsync();}catch(DbUpdateException){throw new BudgetConflictException("This fund changed. Reload before recording activity.");}
        return View(fund);
    }
    internal static async Task SynchronizePaymentAsync(CoinyContext db,int household,Guid key,DateOnly due,BillPayment payment,List<Transaction> entries,DateOnly today)
    {
        var fund=await db.Set<SavingsFund>().Include(f=>f.Activities).SingleOrDefaultAsync(f=>f.HouseholdId==household && f.SourceKey==key);
        if(fund is null) return;
        if(entries.Any(t=>t.AccountId!=fund.AccountId || !(t.Category.CategoryType==CategoryType.Transfer && t.TransferDirection==TransferDirection.In || t.Category.CategoryType==CategoryType.Income)))
            throw new ArgumentException("Link the incoming transfer into this fund's savings account. Record fund-backed expenses as withdrawals from Savings funds.");
        var incomingIds=entries.SelectMany(t=>t.PairedTransactionId.HasValue?new[]{t.Id,t.PairedTransactionId.Value}:new[]{t.Id}).ToArray();
        if(await db.Set<SavingsActivity>().AnyAsync(a=>!a.Kind.StartsWith("Voided:") && a.TransactionId.HasValue && incomingIds.Contains(a.TransactionId.Value) && !(a.SavingsFundId==fund.Id && a.Kind=="CalendarContribution" && a.DueDate==due)))
            throw new ArgumentException("This movement is already allocated to a fund. Manage the existing fund activity first.");
        var prior=fund.Activities.Where(a=>a.Kind=="CalendarContribution" && a.DueDate==due).ToList();
        var manual=fund.Activities.Where(a=>a.Kind is "Contribution" or "ContributionLinked" && a.DueDate==due).ToList();
        if(manual.Count>0) throw new ArgumentException("This contribution was recorded from Savings funds. Correct its activity there before changing the calendar payment.");
        db.RemoveRange(prior);
        foreach(var a in prior) fund.Activities.Remove(a);
        if(payment.IsPaid && entries.Count>0)
        {
            var plan=Plan(fund);
            var weights=plan.Components.Select(c=>SavingsGoalCalculator.Between(plan,c,due.AddDays(-31),due)).ToArray();
            if(weights.Sum()==0) weights=plan.Components.Select(_=>1m).ToArray();
            foreach(var t in entries)
            {
                decimal cumulative=0,previous=0;
                for(var i=0;i<plan.Components.Count;i++)
                {
                    cumulative+=weights[i];var amount=decimal.Round(t.Amount*cumulative/weights.Sum(),2,MidpointRounding.AwayFromZero)-previous;previous+=amount;
                    if(amount>0) fund.Activities.Add(new(){ComponentKey=plan.Components[i].Key,Date=DateOnly.FromDateTime(t.TransactionDate),Amount=amount,Kind="CalendarContribution",TransactionId=t.Id,DueDate=due,OperationId=Guid.NewGuid()});
                }
            }
        }
        if(fund.Activities.GroupBy(a=>a.ComponentKey).Any(g=>g.Sum(a=>a.Amount)<0))
            throw new ArgumentException("Some of this contribution has already been used. Correct or reallocate the later withdrawals before unlinking it.");
        var account=await db.Accounts.SingleAsync(a=>a.Id==fund.AccountId);
        var ledger=await db.Transactions.Include(t=>t.Category).Where(t=>t.AccountId==fund.AccountId && t.TransactionDate<=today.ToDateTime(TimeOnly.MaxValue)).ToListAsync();
        var cash=account.OpeningBalance+ledger.Sum(t=>AccountBalanceCalculator.GetBalanceChange(account.AccountType,t.Category.CategoryType,t.Amount,t.TransferDirection));
        var others=await db.Set<SavingsActivity>().Where(a=>a.SavingsFund.AccountId==fund.AccountId && a.SavingsFundId!=fund.Id).Select(a=>a.Amount).ToListAsync();
        if(Balance(fund)+others.Sum()>cash && entries.Sum(t=>t.Amount)>prior.Sum(a=>a.Amount))throw new ArgumentException("This deposit is already allocated elsewhere in the account. Move existing allocations instead of funding it twice.");
        fund.EditVersion++;
    }
    public async Task<object> CandidatesAsync(Guid key,string kind)
    {
        var fund=await Owned(key);var cutoff=Today.ToDateTime(TimeOnly.MaxValue);
        return await db.Transactions.AsNoTracking().Where(t=>t.HouseholdId==fund.HouseholdId && t.TransactionDate<=cutoff &&
            (kind=="Contribution" ? t.AccountId==fund.AccountId && (t.Category.CategoryType==CategoryType.Income || t.Category.CategoryType==CategoryType.Transfer && t.TransferDirection==TransferDirection.In) : t.Category.CategoryType==CategoryType.Expense))
            .OrderByDescending(t=>t.TransactionDate).ThenByDescending(t=>t.Id).Take(100).Select(t=>new {t.Id,Date=DateOnly.FromDateTime(t.TransactionDate),t.Amount,t.Merchant,t.Description,AccountName=t.Account.Name}).ToListAsync();
    }

    public async Task<SavingsFundView> MoveAsync(Guid key,MoveSavingsAllocation r)
    {
        await using var unit=await db.Database.BeginTransactionAsync();var origin=await Owned(key);
        var destination=r.DestinationFundKey.HasValue?await Owned(r.DestinationFundKey.Value):null;
        if(r.OperationId==Guid.Empty || r.Amount<=0) throw new ArgumentException("Choose a positive amount and an operation identifier.");
        SavingsGoalCalculator.Money(r.Amount);
        var prior=origin.Activities.Where(a=>a.OperationId==r.OperationId).ToList();
        if(prior.Count>0)
        {
            var counterpart=await db.Set<SavingsActivity>().Include(a=>a.SavingsFund).Where(a=>a.OperationId==r.OperationId && a.SavingsFundId!=origin.Id).ToListAsync();
            if(prior.Sum(a=>a.Amount)!=-r.Amount || prior.Any(a=>a.ComponentKey!=r.ComponentKey) || (r.DestinationFundKey.HasValue ? counterpart.Count!=1 || counterpart[0].SavingsFund.SourceKey!=r.DestinationFundKey || counterpart[0].ComponentKey!=r.DestinationComponentKey || counterpart[0].Amount!=r.Amount : counterpart.Count!=0))throw new BudgetConflictException("This operation identifier was already used.");
            return View(origin);
        }
        if(origin.EditVersion!=r.ExpectedEditVersion || destination is not null && destination.EditVersion!=r.DestinationEditVersion)throw new BudgetConflictException("These allocations changed. Reload the funds.");
        if(origin.IsArchived || destination?.IsArchived==true || origin.Id==destination?.Id || destination is not null && destination.AccountId!=origin.AccountId)throw new ArgumentException("Move between different active funds in the same savings account, or release cash to unallocated.");
        if(!Plan(origin).Components.Any(c=>c.Key==r.ComponentKey) || Balance(origin,r.ComponentKey)<r.Amount)throw new ArgumentException("Choose a component with enough allocated money.");
        if(destination is not null && !Plan(destination).Components.Any(c=>c.Key==r.DestinationComponentKey))throw new ArgumentException("Choose a destination component.");
        origin.Activities.Add(new(){ComponentKey=r.ComponentKey,Amount=-r.Amount,Date=Today,Kind="AllocationMove",Note=destination is null?"Released to unallocated cash":"Moved to "+destination.Name,OperationId=r.OperationId});origin.EditVersion++;
        if(destination is not null){destination.Activities.Add(new(){ComponentKey=r.DestinationComponentKey!.Value,Amount=r.Amount,Date=Today,Kind="AllocationMove",Note="Moved from "+origin.Name,OperationId=r.OperationId});destination.EditVersion++;}
        try{await db.SaveChangesAsync();await unit.CommitAsync();}catch(DbUpdateException){throw new BudgetConflictException("These allocations changed. Reload the funds.");}
        return View(origin);
    }
    public async Task ArchiveAsync(Guid key,ArchiveSavingsFund request)
    {
        await using var unit=await db.Database.BeginTransactionAsync();var fund=await Owned(key);
        if(fund.EditVersion!=request.ExpectedEditVersion)throw new BudgetConflictException("This fund changed. Reload it before archiving.");
        if(Balance(fund)!=0)throw new ArgumentException("Move or release the remaining allocated money before archiving this fund.");
        if(request.DraftId is int draftId)
        {
            var draft=await budgets.GetDraftAsync();
            if(draft?.Id!=draftId || draft.EditVersion!=request.DraftEditVersion)throw new BudgetConflictException("The budget draft changed. Reload before archiving.");
            await budgets.SaveDraftAsync(draftId,new(){FundManaged=true,ExpectedEditVersion=draft.EditVersion,EffectiveFromMonth=draft.EffectiveFromMonth,SetupStep=draft.SetupStep,IncomeSources=draft.IncomeSources.ToList(),Allocations=draft.Allocations.Where(a=>a.SourceKey!=key).ToList()});
        }
        else await budgets.ChangeItemAsync(new(){SourceKey=key,FundManaged=true,Remove=true,ExpectedRevisionId=request.ExpectedRevisionId,EffectiveDate=request.EffectiveDate},false);
        fund.IsArchived=true;fund.EditVersion++;await db.SaveChangesAsync();await unit.CommitAsync();
    }
    public async Task<SavingsFundView> CorrectActivityAsync(Guid key,Guid operation,int version)
    {
        await using var unit=await db.Database.BeginTransactionAsync();var fund=await Owned(key);
        if(fund.EditVersion!=version)throw new BudgetConflictException("This fund changed. Reload it before correcting activity.");
        var entries=fund.Activities.Where(a=>a.OperationId==operation).ToList();
        if(entries.Count==0 || entries.Any(a=>a.Kind is not ("Contribution" or "ContributionLinked" or "Withdrawal" or "WithdrawalLinked" or "Reallocate" or "Allocate")))throw new ArgumentException("Correct calendar contributions through their payment link, or move opening allocations between purposes.");
        foreach(var group in entries.GroupBy(a=>a.ComponentKey))if(Balance(fund,group.Key)-group.Sum(a=>a.Amount)<0)throw new ArgumentException("Correct later withdrawals first; this correction would leave a negative component balance.");
        var correction=Guid.NewGuid();
        foreach(var entry in entries){fund.Activities.Add(new(){ComponentKey=entry.ComponentKey,Date=Today,Amount=-entry.Amount,Kind="Correction",Note="Reverses activity #"+entry.Id,OperationId=correction});entry.Kind="Voided:"+entry.Kind;}
        foreach(var due in entries.Where(a=>a.DueDate.HasValue).Select(a=>a.DueDate!.Value).Distinct())
        {
            var payment=await db.BillPayments.SingleOrDefaultAsync(p=>p.HouseholdId==fund.HouseholdId && p.AllocationKey==key && p.DueDate==due);
            if(payment is not null && payment.TransactionId is null && !fund.Activities.Any(a=>a.DueDate==due && a.Kind is "Contribution" or "ContributionLinked")){payment.IsPaid=false;payment.PaidOn=null;}
        }
        fund.EditVersion++;await db.SaveChangesAsync();await unit.CommitAsync();return View(fund);
    }

}
