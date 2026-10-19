using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;
public class BillPaymentTests
{
    private static readonly DateOnly Due = new(2026,10,17);
    private sealed class User(int id) : ICurrentUserService {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(id);
    }
    private static BillPaymentService Service(BudgetTestDatabase db, int household = 1) => new(db.Context(), new User(household), db.Service(household), db.Clock);
    private static async Task<Guid> Plan(BudgetTestDatabase db)
    {
        var service = db.Service(); var draft = await service.StartDraftAsync();
        var saved = await service.SaveDraftAsync(draft.Id, new() {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026,10,1), SetupStep = BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 2000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026,10,16) } }],
            Allocations = [new() { Name = "Electric", Amount = 100, AllocationType = BudgetAllocationType.Bills,
                Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 17 } }],
        });
        await service.PublishDraftAsync(saved.Id, saved.EditVersion);
        return saved.Allocations[0].SourceKey!.Value;
    }
    private static async Task Ledger(BudgetTestDatabase db)
    {
        var c = db.Context();
        c.Accounts.AddRange(new Account { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalance = 1000, OpeningBalanceDate = new(2026,10,1) }, new Account { Id = 2, HouseholdId = 2, Name = "Other" });
        c.Categories.AddRange(new Category { Id = 1, HouseholdId = 1, Name = "Utilities", CategoryType = CategoryType.Expense }, new Category { Id = 2, HouseholdId = 1, Name = "Salary", CategoryType = CategoryType.Income }, new Category { Id = 3, HouseholdId = 2, Name = "Other", CategoryType = CategoryType.Expense });
        c.Transactions.AddRange(new Transaction { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, TransactionDate = new(2026,10,15), Description = "Electric payment" },
            new Transaction { Id = 2, HouseholdId = 1, AccountId = 1, CategoryId = 2, Amount = 100, TransactionDate = new(2026,10,15), Description = "Income" },
            new Transaction { Id = 3, HouseholdId = 2, AccountId = 2, CategoryId = 3, Amount = 100, TransactionDate = new(2026,10,15), Description = "Other" });
        await c.SaveChangesAsync();
    }
    [Fact] public async Task Manual_payment_is_occurrence_specific_and_reduces_only_unpaid_total()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db);
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true, PaidOn = new(2026,10,16), Note = " Paid online " });
        var bill = Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome);
        Assert.True(bill.Payment!.IsPaid); Assert.Equal("Paid online", bill.Payment.Note);
        Assert.Null(Assert.Single((await db.Service().GetMonthAsync(new(2026,11,1))).Events, e => !e.IsIncome).Payment);
        var period = await db.Service().GetIncomePeriodAsync(); Assert.Equal(100, period.BillsTotal); Assert.Equal(0, period.UnpaidBillsTotal);
        Assert.Empty(await db.Context().Transactions.ToListAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
        await Service(db).SaveAsync(key, Due, new() { IsPaid = false });
        Assert.Equal(100, (await db.Service().GetIncomePeriodAsync()).UnpaidBillsTotal);
    }
    [Fact] public async Task Link_then_unlink_keeps_paid_status_and_never_changes_ledger()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db); await Ledger(db);
        var linked = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 1 });
        Assert.Equal(new DateOnly(2026,10,15), linked.PaidOn);
        Assert.Equal("Electric payment", linked.LinkedTransaction!.Description);
        Assert.NotNull(Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome).Payment!.LinkedTransaction);
        var unlinked = await Service(db).SaveAsync(key, Due, new() { IsPaid = true, PaidOn = linked.PaidOn });
        Assert.True(unlinked.IsPaid); Assert.Null(unlinked.TransactionId); Assert.Null(unlinked.LinkedTransaction);
        Assert.Equal(3, await db.Context().Transactions.CountAsync()); Assert.Empty(await db.Context().AccountBalanceSnapshots.ToListAsync());
        Assert.Equal(1, Assert.Single(await Service(db).CandidatesAsync(key, Due, null)).Id);
    }
    [Fact] public async Task Invalid_foreign_income_and_already_used_links_are_rejected()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db); await Ledger(db);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db, 2).SaveAsync(key, Due, new() { IsPaid = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due.AddDays(1), new() { IsPaid = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 3 }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 2 }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db).SaveAsync(key, Due, new() { IsPaid = true, PaidOn = Due }));
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true, TransactionId = 1 });
        await Assert.ThrowsAsync<BudgetConflictException>(() => Service(db).SaveAsync(key, Due.AddMonths(1), new() { IsPaid = true, TransactionId = 1 }));
        Assert.Empty(await Service(db).CandidatesAsync(key, Due.AddMonths(1), null));
        Assert.Single(await db.Context().BillPayments.ToListAsync());
    }
    [Fact] public async Task Stable_identity_survives_draft_saves_rename_and_republication()
    {
        await using var db = await BudgetTestDatabase.CreateAsync(); var key = await Plan(db);
        await Service(db).SaveAsync(key, Due, new() { IsPaid = true });
        var draft = await db.Service().StartDraftAsync(); Assert.Equal(key, draft.Allocations[0].SourceKey);
        draft.Allocations[0].Name = "Renamed electric";
        var saved = await db.Service().SaveDraftAsync(draft.Id, new() { ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = draft.EffectiveFromMonth, SetupStep = BudgetSetupStep.Review, IncomeSources = draft.IncomeSources.ToList(), Allocations = draft.Allocations.ToList() });
        await db.Service().PublishDraftAsync(saved.Id, saved.EditVersion);
        var bill = Assert.Single((await db.Service().GetMonthAsync(new(2026,10,1))).Events, e => !e.IsIncome);
        Assert.Equal("Renamed electric", bill.Name); Assert.True(bill.Payment!.IsPaid);
    }
}
