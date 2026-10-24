using Coiny.API.Data;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class BudgetActualsTests
{
    private static readonly DateOnly Month = new(2026, 10, 1);
    private sealed class User(int household = 1) : ICurrentUserService
    {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    private static BudgetActualsService Service(BudgetTestDatabase db, int household = 1) => new(db.Context(), new User(household), db.Service(household), db.Clock);
    private static async Task<Dictionary<string, Guid>> Seed(BudgetTestDatabase db)
    {
        db.Clock.Now = new(2026, 9, 16, 16, 0, 0, TimeSpan.Zero);
        var budgets = db.Service(); var draft = await budgets.StartDraftAsync();
        var saved = await budgets.SaveDraftAsync(draft.Id, new()
        {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026, 9, 1), SetupStep = BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 2000, Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026, 9, 1), DayOfMonth = 1 } }],
            Allocations = [new() { Name = "Groceries", Amount = 300, AllocationType = BudgetAllocationType.Spending },
                new() { Name = "Electric", Amount = 100, AllocationType = BudgetAllocationType.Bills, Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026, 9, 1), DayOfMonth = 15 } },
                new() { Name = "IRA", Amount = 200, AllocationType = BudgetAllocationType.Investments, Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026, 9, 1), DayOfMonth = 15 } }]
        });
        await budgets.PublishDraftAsync(saved.Id, saved.EditVersion);
        db.Clock.Now = new(2026, 10, 16, 16, 0, 0, TimeSpan.Zero);
        var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalance = 1000, OpeningBalanceDate = new(2026, 9, 1) },
            new Account { Id = 2, HouseholdId = 1, Name = "IRA", AccountType = AccountType.Investment, OpeningBalanceDate = new(2026, 9, 1) },
            new Account { Id = 3, HouseholdId = 2, Name = "Foreign", OpeningBalanceDate = new(2026, 9, 1) });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Food", CategoryType = CategoryType.Expense, IsActive = true },
            new Category { Id = 2, HouseholdId = 1, Name = "Pay", CategoryType = CategoryType.Income, IsActive = true },
            new Category { Id = 3, HouseholdId = 2, Name = "Other", CategoryType = CategoryType.Expense, IsActive = true });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 40, TransactionDate = new(2026, 10, 5), Description = "Groceries" },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026, 10, 15), Description = "Electric" },
            new Transaction { Id = 3, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 99, TransactionDate = new(2026, 10, 17), Description = "Future" },
            new Transaction { Id = 4, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 60, TransactionDate = new(2026, 9, 30), Description = "Earlier" },
            new Transaction { Id = 5, HouseholdId = 1, AccountId = 1, CategoryId = 2, Amount = 2000, TransactionDate = new(2026, 10, 1), Description = "Income" },
            new Transaction { Id = 6, HouseholdId = 2, AccountId = 3, CategoryId = 3, Amount = 500, TransactionDate = new(2026, 10, 1), Description = "Foreign" });
        await c.SaveChangesAsync();
        return saved.Allocations.ToDictionary(a => a.Name, a => a.SourceKey!.Value);
    }
    private static async Task Assign(BudgetTestDatabase db, int id, Guid? key, bool reset = false, DateOnly? month = null)
    {
        var entry = await db.Context().Transactions.AsNoTracking().SingleAsync(t => t.Id == id);
        var pairVersion = entry.PairedTransactionId is int pair ? (int?)(await db.Context().Transactions.AsNoTracking().SingleAsync(t => t.Id == pair)).EditVersion : null;
        await Service(db).SaveAssignmentAsync(id, new() { Month = month ?? Month, AllocationKey = key, UseCategoryDefault = reset, ExpectedEditVersion = entry.EditVersion, ExpectedPairedEditVersion = pairVersion });
    }
    [Fact] public async Task Defaults_payment_links_and_overrides_have_one_assignment_per_transaction_and_pending_is_separate()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        await Service(db).SaveCategoryAsync(1, new() { Month = Month, AllocationKey = keys["Groceries"] });
        var payments = new BillPaymentService(db.Context(), new User(), db.Service(), db.Clock);
        await payments.SaveAsync(keys["Electric"], new(2026, 10, 15), new() { IsPaid = true, TransactionId = 2 });
        await payments.SaveAsync(keys["IRA"], new(2026, 10, 15), new() { IsPaid = true });
        var report = await Service(db).GetAsync(Month, "all");
        Assert.Equal(140, report.AssignedActual); Assert.Equal(0, report.UnassignedExpenses); Assert.Equal(200, report.AwaitingTransaction);
        Assert.Equal(40, Assert.Single(report.Items, a => a.Name == "Groceries").Actual); Assert.Equal(100, Assert.Single(report.Items, a => a.Name == "Electric").Actual);
        Assert.Equal(2, report.TotalCount); Assert.Equal("Payment", Assert.Single(report.Transactions, t => t.Id == 2).AssignmentSource);
        var detail = await Service(db).GetAsync(Month, allocationKey: keys["Electric"]);
        Assert.Equal(1, detail.TotalCount);
        Assert.Equal(100, Assert.Single(detail.Transactions).Amount);
        Assert.Equal(keys["Electric"], Assert.Single(detail.Transactions).AllocationKey);
        Assert.Equal(report.Points, detail.Points); // Item filtering does not alter the monthly graph.
        Assert.Empty((await Service(db).GetAsync(Month, allocationKey: keys["IRA"])).Transactions);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).GetAsync(Month, allocationKey: Guid.NewGuid()));
        Assert.Equal(31, report.Points.Count);
        Assert.Equal(400, report.Points[^1].PlannedSpending); Assert.Equal(200, report.Points[^1].PlannedContributions);
        Assert.Equal(140, report.Points[15].ActualSpending); Assert.Equal(0, report.Points[15].ActualContributions);
        Assert.Null(report.Points[16].ActualSpending); Assert.Null(report.Points[30].ActualContributions);
        Assert.Equal(135.48m, report.Points[13].PlannedSpending); // Only the monthly allowance before the bill is due.
        Assert.Equal(245.16m, report.Points[14].PlannedSpending); // The scheduled bill enters on the 15th.
        await Assign(db, 1, keys["Electric"]); report = await Service(db).GetAsync(Month);
        Assert.Equal(140, Assert.Single(report.Items, a => a.Name == "Electric").Actual); Assert.Equal(-40, Assert.Single(report.Items, a => a.Name == "Electric").Remaining);
        await Assign(db, 1, null); report = await Service(db).GetAsync(Month); Assert.Equal(40, report.UnassignedExpenses);
        await Assign(db, 1, null, true); Assert.Equal(140, (await Service(db).GetAsync(Month)).AssignedActual);
        Assert.Equal(6, await db.Context().Transactions.CountAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
    }
    [Fact] public async Task Linked_payments_identify_due_dates_outside_the_report_without_moving_posted_actuals()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        var payments = new BillPaymentService(db.Context(), new User(), db.Service(), db.Clock);
        await payments.SaveAsync(keys["Electric"],new(2026,9,15),new() { IsPaid=true,TransactionId=2 });
        await payments.SaveAsync(keys["IRA"],new(2026,11,15),new() { IsPaid=true,TransactionId=1 });
        var report=await Service(db).GetAsync(Month,"all");
        var electric=Assert.Single(report.Items,i=>i.Name=="Electric");
        Assert.Equal(100,electric.Actual); Assert.Equal(100,electric.PriorPeriodActual); Assert.Equal(0,electric.FuturePeriodActual);
        var ira=Assert.Single(report.Items,i=>i.Name=="IRA");
        Assert.Equal(40,ira.Actual); Assert.Equal(40,ira.FuturePeriodActual);
        Assert.Equal(new DateOnly(2026,9,15),Assert.Single(report.Transactions,t=>t.Id==2).PaymentDueDate);
        Assert.Equal(140,report.AssignedActual); Assert.Equal(100,report.Points[15].ActualSpending); Assert.Equal(40,report.Points[15].ActualContributions);
        Assert.Equal(0,(await Service(db).GetAsync(new(2026,9,1),"all")).AssignedActual);
        var samePeriod=await Service(db).GetPeriodAsync(new(2026,10,16),"all");
        Assert.Equal(100,Assert.Single(samePeriod.Items,i=>i.Name=="Electric").PriorPeriodActual);
    }
    [Fact] public async Task Forecast_anchors_to_actuals_replaces_linked_plan_amounts_and_includes_pending_commitments_once()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        await Assign(db,1,keys["Groceries"]);
        var c=db.Context(); var transaction=await c.Transactions.SingleAsync(t=>t.Id==2); transaction.Amount=120; await c.SaveChangesAsync();
        var payments=new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);
        await payments.SaveAsync(keys["Electric"],new(2026,10,15),new() { IsPaid=true,TransactionId=2 });
        await payments.SaveAsync(keys["IRA"],new(2026,10,15),new() { IsPaid=true });
        var report=await Service(db).GetAsync(Month,"all");
        var anchor=report.Points[15]; Assert.Equal(160,anchor.ActualSpending); Assert.Equal(anchor.ActualSpending,anchor.ForecastSpending);
        Assert.Equal(anchor.ActualContributions,anchor.ForecastContributions);
        Assert.All(report.Points.Take(15),p=>Assert.Null(p.ForecastSpending));
        Assert.Equal(200,report.Points[16].ForecastContributions); // Awaiting transaction, estimated next day.
        Assert.Equal(420,report.Points[^1].ForecastSpending); // Actual 160 + unused groceries 260; no second electric bill.
        Assert.Equal(200,report.Points[^1].ForecastContributions);
        Assert.Null(report.Points[16].ActualSpending);
        Assert.Equal(report.Points,(await Service(db).GetAsync(Month,allocationKey:keys["Electric"])).Points);
        Assert.Equal(6,await db.Context().Transactions.CountAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
    }
    [Fact] public async Task Forecast_retains_unpaid_overdue_items_but_does_not_double_count_individually_assigned_actuals()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        await Assign(db,1,keys["Groceries"]);
        var unpaid=await Service(db).GetAsync(Month,"all");
        Assert.Equal(500,unpaid.Points[^1].ForecastSpending); // Unassigned electric actual remains actual; overdue bill still needs review.
        await Assign(db,2,keys["Electric"]);
        var assigned=await Service(db).GetAsync(Month,"all");
        Assert.Equal(400,assigned.Points[^1].ForecastSpending);
        Assert.Equal(200,assigned.Points[^1].ForecastContributions);
        var future=await Service(db).GetAsync(new(2026,11,1)); Assert.All(future.Points,p=>Assert.Null(p.ForecastSpending));
        var past=await Service(db).GetAsync(new(2026,9,1)); Assert.All(past.Points,p=>Assert.Null(p.ForecastSpending));
    }
    [Fact] public async Task Forecast_respects_removed_allowances_and_does_not_spend_their_unused_historical_budget()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        await Assign(db,1,keys["Groceries"]);
        var payments=new BillPaymentService(db.Context(),new User(),db.Service(),db.Clock);
        await payments.SaveAsync(keys["Electric"],new(2026,10,15),new() { IsPaid=true,TransactionId=2 });
        var management=await db.Service().GetManagementAsync();
        await db.Service().ChangeItemAsync(new() { ExpectedRevisionId=management.Revision.Id,SourceKey=keys["Groceries"],EffectiveDate=new(2026,10,16),Remove=true },false);
        var report=await Service(db).GetAsync(Month,"all");
        Assert.Equal(140,report.Points[^1].ForecastSpending);
        Assert.Equal(report.Points[15].ActualSpending,report.Points[15].ForecastSpending);
    }
    [Fact] public async Task Rules_are_effective_dated_stale_writes_rejected_and_future_month_has_no_actuals()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        await Service(db).SaveCategoryAsync(1, new() { Month = new(2026, 9, 1), AllocationKey = keys["Groceries"] });
        await Service(db).SaveCategoryAsync(1, new() { Month = Month, AllocationKey = keys["Electric"] });
        Assert.Equal(60, Assert.Single((await Service(db).GetAsync(new(2026, 9, 1))).Items, a => a.Name == "Groceries").Actual);
        Assert.Equal(140, Assert.Single((await Service(db).GetAsync(Month)).Items, a => a.Name == "Electric").Actual);
        await Assert.ThrowsAsync<BudgetConflictException>(() => Service(db).SaveCategoryAsync(1, new() { Month = Month, AllocationKey = keys["IRA"], ExpectedEditVersion = 0 }));
        var future = await Service(db).GetAsync(new(2026, 11, 1)); Assert.Null(future.ThroughDate); Assert.Equal(0, future.AssignedActual); Assert.Empty(future.Transactions);
        Assert.Equal(30, future.Points.Count); Assert.All(future.Points, p => Assert.Null(p.ActualSpending)); Assert.Equal(400, future.Points[^1].PlannedSpending);
    }
    [Fact] public async Task Transfers_require_explicit_contribution_assignment_and_paired_movements_count_once_on_incoming_date()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db); var c = db.Context();
        var transfers = new TransferService(c, new User(), new AccountBalanceSnapshotService(c, new User()));
        var outgoing = await transfers.CreateEntryAsync(new() { AccountId = 1, Amount = 200, Date = new(2026, 9, 30), Direction = TransferDirection.Out, Description = "Contribution" });
        var incoming = await transfers.CreateEntryAsync(new() { AccountId = 2, Amount = 200, Date = new(2026, 10, 1), Direction = TransferDirection.In, Description = "Contribution received" });
        await transfers.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = incoming.Id });
        Assert.Equal(0, (await Service(db).GetAsync(Month)).AssignedActual);
        await Assert.ThrowsAsync<ArgumentException>(() => Assign(db, incoming.Id, keys["Groceries"]));
        await Assign(db, outgoing.Id, keys["IRA"]);
        var report = await Service(db).GetAsync(Month, "all", "Transfer"); Assert.Single(report.Transactions); Assert.Equal(200, report.AssignedActual);
        Assert.Equal(200, report.Points[15].ActualContributions); Assert.Equal(140, report.Points[15].ActualSpending); // Unassigned expenses remain visible in spending.
        Assert.Equal(0, (await Service(db).GetAsync(new(2026, 9, 1))).AssignedActual);
        Assert.Equal(8, await c.Transactions.CountAsync());
    }
    [Fact] public async Task Invalid_foreign_income_and_payment_assignments_are_rejected_and_versions_protect_overrides()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db, 2).GetAsync(Month));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveCategoryAsync(3, new() { Month = Month, AllocationKey = keys["IRA"] }));
        await Assert.ThrowsAsync<ArgumentException>(() => Assign(db, 5, keys["Groceries"]));
        await Assert.ThrowsAsync<ArgumentException>(() => Assign(db, 1, Guid.NewGuid()));
        await Assign(db, 1, keys["Groceries"]);
        await Assert.ThrowsAsync<BudgetConflictException>(() => Service(db).SaveAssignmentAsync(1, new() { Month = Month, AllocationKey = keys["IRA"], ExpectedEditVersion = 0 }));
        var payments = new BillPaymentService(db.Context(), new User(), db.Service(), db.Clock);
        await Assert.ThrowsAsync<BudgetConflictException>(() => payments.SaveAsync(keys["Electric"], new(2026, 10, 15), new() { IsPaid = true, TransactionId = 1 }));
        await payments.SaveAsync(keys["Electric"], new(2026, 10, 15), new() { IsPaid = true, TransactionId = 2 });
        await Assert.ThrowsAsync<BudgetConflictException>(() => Assign(db, 2, keys["Groceries"]));
    }
    [Fact] public async Task Linked_bill_uses_posting_month_even_when_due_in_another_month()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db);
        var payments = new BillPaymentService(db.Context(), new User(), db.Service(), db.Clock);
        await payments.SaveAsync(keys["Electric"], new(2026, 11, 15), new() { IsPaid = true, TransactionId = 2 });
        Assert.Equal(100, Assert.Single((await Service(db).GetAsync(Month)).Items, i => i.Name == "Electric").Actual);
        Assert.Equal(0, (await Service(db).GetAsync(new(2026, 11, 1))).AssignedActual);
    }
    [Fact] public async Task Assignment_failure_rolls_back_review_versions_and_assignment_rows()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db); var c = db.Context();
        await c.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailBudgetAssignment BEFORE INSERT ON TransactionBudgetAssignments BEGIN SELECT RAISE(ABORT, 'Test failure'); END;");
        await Assert.ThrowsAsync<BudgetConflictException>(() => Assign(db, 1, keys["Groceries"]));
        Assert.Equal(0, (await db.Context().Transactions.AsNoTracking().SingleAsync(t => t.Id == 1)).EditVersion);
        Assert.Empty(await db.Context().TransactionBudgetAssignments.ToListAsync());
        Assert.Equal(140, (await Service(db).GetAsync(Month)).UnassignedExpenses);
    }
    [Fact] public async Task Two_individually_assigned_transfer_entries_cannot_be_paired_and_counted_twice()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var keys = await Seed(db); var c = db.Context();
        var transfers = new TransferService(c, new User(), new AccountBalanceSnapshotService(c, new User()));
        var outgoing = await transfers.CreateEntryAsync(new() { AccountId = 1, Amount = 200, Date = new(2026, 10, 15), Direction = TransferDirection.Out, Description = "Out" });
        var incoming = await transfers.CreateEntryAsync(new() { AccountId = 2, Amount = 200, Date = new(2026, 10, 15), Direction = TransferDirection.In, Description = "In" });
        await Assign(db, outgoing.Id, keys["IRA"]); await Assign(db, incoming.Id, keys["IRA"]);
        await Assert.ThrowsAsync<TransferConflictException>(() => transfers.LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = incoming.Id }));
        await Assign(db, outgoing.Id, null, true);
        var fresh = db.Context(); await new TransferService(fresh, new User(), new AccountBalanceSnapshotService(fresh, new User())).LinkAsync(new() { FirstTransactionId = outgoing.Id, SecondTransactionId = incoming.Id });
        Assert.Equal(200, (await Service(db).GetAsync(Month)).AssignedActual);
    }
}
