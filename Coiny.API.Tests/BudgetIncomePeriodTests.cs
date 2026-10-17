using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class BudgetIncomePeriodTests
{
    private static BudgetDraftRequest Request(int version = 0) => new() {
        ExpectedEditVersion = version, EffectiveFromMonth = new(2026,10,1), SetupStep = BudgetSetupStep.Review,
        IncomeSources = [
            new() { Name = "Salary A", Amount = 2000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026,10,2) } },
            new() { Name = "Salary B", Amount = 1000, Schedule = new() { Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026,10,9) } },
        ],
        Allocations = [
            Bill("Payday bill", 16, 50), Bill("Electric", 17, 100), Bill("Next payday bill", 23, 150), Bill("Month end", 31, 200),
            new() { Name = "Savings", Amount = 25, AllocationType = BudgetAllocationType.Savings,
                Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 18 } },
            new() { Name = "Groceries", Amount = 500, AllocationType = BudgetAllocationType.Spending },
        ],
    };
    private static BudgetAllocationInput Bill(string name, int day, decimal amount) => new() {
        Name = name, Amount = amount, AllocationType = BudgetAllocationType.Bills,
        Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = day },
    };
    private static async Task<BudgetRevisionResponse> Publish(BudgetTestDatabase db, BudgetDraftRequest? request = null)
    {
        var draft = await db.Service().StartDraftAsync();
        var saved = await db.Service().SaveDraftAsync(draft.Id, request ?? Request(draft.EditVersion));
        return await db.Service().PublishDraftAsync(saved.Id, saved.EditVersion);
    }

    [Fact]
    public async Task HouseholdAndIndividualPeriodsSplitAtDistinctPaydaysAndSeparateContributions()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var published = await Publish(db);
        var household = await db.Service().GetIncomePeriodAsync();
        Assert.Equal(new DateOnly(2026,10,16), household.StartDate);
        Assert.Equal(new DateOnly(2026,10,22), household.EndDate);
        Assert.Equal(new DateOnly(2026,10,23), household.NextPayday);
        Assert.Equal(150, household.BillsTotal);
        Assert.Equal(25, household.ContributionsTotal);
        Assert.DoesNotContain(household.Bills, b => b.Name is "Next payday bill" or "Groceries");
        var individual = await db.Service().GetIncomePeriodAsync(sourceKey: published.IncomeSources[0].SourceKey);
        Assert.Equal(new DateOnly(2026,10,29), individual.EndDate);
        Assert.Equal(300, individual.BillsTotal);
        Assert.Equal(2, individual.Sources.Count);
        Assert.Empty(await db.Context().Transactions.ToListAsync());
    }

    [Fact]
    public async Task PeriodsCrossMonthBoundariesAndRespectFuturePublishedScheduleChanges()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var original = await Publish(db);
        var key = original.IncomeSources[0].SourceKey;
        var future = await db.Service().StartDraftAsync(new(2026,11,1));
        Assert.Equal(key, future.IncomeSources[0].SourceKey);
        var change = new BudgetDraftRequest {
            ExpectedEditVersion = future.EditVersion, EffectiveFromMonth = future.EffectiveFromMonth, SetupStep = BudgetSetupStep.Review,
            IncomeSources = future.IncomeSources.ToList(), Allocations = future.Allocations.ToList(),
        };
        change.IncomeSources[0].Schedule.StartDate = new(2026,11,5);
        future = await db.Service().SaveDraftAsync(future.Id, change);
        await db.Service().PublishDraftAsync(future.Id, future.EditVersion);
        var period = await db.Service().GetIncomePeriodAsync(new(2026,11,1), key);
        Assert.Equal(new DateOnly(2026,10,30), period.StartDate);
        Assert.Equal(new DateOnly(2026,11,4), period.EndDate);
        Assert.Equal(new DateOnly(2026,11,5), period.NextPayday);
        Assert.Equal("Month end", Assert.Single(period.Bills).Name);
        Assert.Equal(200, period.BillsTotal);
    }

    [Fact]
    public async Task MissingAndEndedIncomeSchedulesHaveNoInventedPeriodAndHouseholdsStayIsolated()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        Assert.Null((await db.Service().GetIncomePeriodAsync()).StartDate);
        var request = Request();
        request.IncomeSources.RemoveAt(1);
        request.IncomeSources[0].Schedule.EndDate = new(2026,10,16);
        var published = await Publish(db, request);
        var period = await db.Service().GetIncomePeriodAsync();
        Assert.Equal(new DateOnly(2026,10,16), period.StartDate);
        Assert.Null(period.NextPayday); Assert.Null(period.EndDate); Assert.Empty(period.Bills);
        var before = await db.Service().GetIncomePeriodAsync(new(2026,10,1));
        Assert.Null(before.StartDate); Assert.Equal(new DateOnly(2026,10,2), before.NextPayday);
        Assert.Empty((await db.Service(2).GetIncomePeriodAsync()).Sources);
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service(2).GetIncomePeriodAsync(sourceKey: published.IncomeSources[0].SourceKey));
    }

    [Fact]
    public async Task SameDayPaydaysAreOneBoundaryAndNewYorkDateIsUsed()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var request = Request();
        request.IncomeSources[1].Schedule.StartDate = new(2026,10,2);
        await Publish(db, request);
        db.Clock.Now = new(2026,10,17,0,30,0,TimeSpan.Zero);
        var period = await db.Service().GetIncomePeriodAsync();
        Assert.Equal(new DateOnly(2026,10,16), period.AsOf);
        Assert.Equal(new DateOnly(2026,10,30), period.NextPayday);
        Assert.Equal(new DateOnly(2026,10,29), period.EndDate);
    }

    [Fact]
    public async Task SourceKeysSurviveSavesAndDuplicatesAreRejected()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var draft = await db.Service().StartDraftAsync();
        draft = await db.Service().SaveDraftAsync(draft.Id, Request());
        var update = Request(draft.EditVersion);
        update.IncomeSources = draft.IncomeSources.ToList();
        var key = update.IncomeSources[0].SourceKey;
        draft = await db.Service().SaveDraftAsync(draft.Id, update);
        Assert.Equal(key, draft.IncomeSources[0].SourceKey);
        update.ExpectedEditVersion = draft.EditVersion;
        update.IncomeSources[1].SourceKey = key;
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().SaveDraftAsync(draft.Id, update));
    }
}
