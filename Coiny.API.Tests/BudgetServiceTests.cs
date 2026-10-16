using Coiny.API.DTOs.Budgets;
using Coiny.API.Enums;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

public class BudgetServiceTests
{
    [Fact]
    public async Task SetupReadDoesNotCreateBudgetAndReportsCurrentPublishedRevision()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var empty = await db.Service().GetSetupStatusAsync();
        Assert.False(empty.HasPublishedBudget);
        Assert.Null(empty.Draft);
        Assert.Empty(await db.Context().HouseholdBudgets.ToListAsync());
        var published = await Publish(db.Service());
        var draft = await db.Service().StartDraftAsync();
        var setup = await db.Service().GetSetupStatusAsync();
        Assert.Equal(published.Id, setup.CurrentRevisionId);
        Assert.Equal(draft.Id, setup.Draft!.Id);
        Assert.False((await db.Service(2).GetSetupStatusAsync()).HasPublishedBudget);
    }

    [Fact]
    public async Task PreviewRequiresCurrentVersionAndApplicableMonthWithoutPublishing()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var draft = await db.Service().StartDraftAsync();
        draft = await db.Service().SaveDraftAsync(draft.Id, Request());
        await Assert.ThrowsAsync<BudgetConflictException>(() => db.Service().PreviewDraftAsync(draft.Id, 0, new(2026,10,1)));
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().PreviewDraftAsync(draft.Id, 1, new(2026,9,1)));
        var preview = await db.Service().PreviewDraftAsync(draft.Id, 1, new(2026,10,1));
        Assert.Equal(6000, preview.Month.ExpectedIncome);
        Assert.Equal(2700, preview.Month.Allocated);
        Assert.Equal(1, preview.EditVersion);
        Assert.Null((await db.Service().GetMonthAsync(new(2026,10,1))).RevisionId);
        Assert.Equal(BudgetRevisionState.Draft, (await db.Service().GetDraftAsync())!.State);
    }

    [Fact]
    public async Task PaycheckDetailsRemainInformationalAndSurvivePublicationAndCloning()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var draft = await db.Service().StartDraftAsync();
        var request = Request();
        request.IncomeSources[0].HealthInsurancePerPaycheck = 125.25m;
        request.IncomeSources[0].RetirementContributionPerPaycheck = 200;
        request.Allocations.Add(new() { Name = "Car insurance", Amount = 1200, AllocationType = BudgetAllocationType.Bills,
            Schedule = new() { Frequency = BudgetFrequency.EverySixMonths, StartDate = new(2026,10,15) } });
        request.Allocations.Add(new() { Name = "Annual insurance", Amount = 100, AllocationType = BudgetAllocationType.Bills,
            Schedule = new() { Frequency = BudgetFrequency.Yearly, StartDate = new(2026,10,15) } });
        var saved = await db.Service().SaveDraftAsync(draft.Id, request);
        Assert.Equal(125.25m, Assert.Single((await db.Service().GetDraftAsync())!.IncomeSources).HealthInsurancePerPaycheck);
        var preview = (await db.Service().PreviewDraftAsync(draft.Id, saved.EditVersion, new(2026,10,1))).Month;
        Assert.Equal(6000, preview.ExpectedIncome);
        Assert.Equal(4000, preview.Allocated);
        Assert.Equal(2000, preview.Unassigned);
        Assert.Equal(375.75m, preview.HealthInsurance);
        Assert.Equal(600, preview.RetirementContributions);
        await db.Service().PublishDraftAsync(draft.Id, saved.EditVersion);
        var november = await db.Service().GetMonthAsync(new(2026,11,1));
        Assert.Equal(250.50m, november.HealthInsurance);
        Assert.Equal(400, november.RetirementContributions);
        Assert.DoesNotContain(november.Events, e => e.Name.Contains("insurance"));
        Assert.Contains((await db.Service().GetMonthAsync(new(2027,4,1))).Events, e => e.Name == "Car insurance");
        var clone = await db.Service().StartDraftAsync(new(2026,11,1));
        Assert.Equal(200, Assert.Single(clone.IncomeSources).RetirementContributionPerPaycheck);
        var update = Request(clone.EditVersion, new(2026,11,1));
        update.IncomeSources[0].HealthInsurancePerPaycheck = -1;
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().SaveDraftAsync(clone.Id, update));
        update.IncomeSources[0].HealthInsurancePerPaycheck = 0;
        update.IncomeSources[0].RetirementContributionPerPaycheck = .001m;
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().SaveDraftAsync(clone.Id, update));
        Assert.Empty(await db.Context().Transactions.ToListAsync());
    }

    private static BudgetDraftRequest Request(int version = 0, DateOnly? month = null) => new() {
        ExpectedEditVersion = version, EffectiveFromMonth = month ?? new(2026,10,1), SetupStep = BudgetSetupStep.Review,
        IncomeSources = [new() { Name = " Salary ", Amount = 2000, Schedule = new() {
            Frequency = BudgetFrequency.EveryTwoWeeks, StartDate = new(2026,10,2) } }],
        Allocations = [
            new() { Name = "Rent", Amount = 1500, AllocationType = BudgetAllocationType.Housing,
                Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 15 } },
            new() { Name = "Groceries", Amount = 600, AllocationType = BudgetAllocationType.Spending },
            new() { Name = "Savings", Amount = 100, AllocationType = BudgetAllocationType.Savings,
                Schedule = new() { Frequency = BudgetFrequency.Weekly, StartDate = new(2026,10,2) } },
            new() { Name = "Investing", Amount = 100, AllocationType = BudgetAllocationType.Investments,
                Schedule = new() { Frequency = BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 31 } },
        ],
    };
    private static async Task<BudgetRevisionResponse> Publish(BudgetService service)
    {
        var draft = await service.StartDraftAsync();
        var saved = await service.SaveDraftAsync(draft.Id, Request(draft.EditVersion));
        return await service.PublishDraftAsync(saved.Id, saved.EditVersion);
    }

    [Fact]
    public async Task DraftResumesAcrossRequestsAndDoesNotAffectMonthlyResults()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        Assert.Null(await db.Service().GetDraftAsync());
        var draft = await db.Service().StartDraftAsync();
        var request = new BudgetDraftRequest { ExpectedEditVersion = draft.EditVersion,
            EffectiveFromMonth = draft.EffectiveFromMonth, SetupStep = BudgetSetupStep.Bills };
        var saved = await db.Service().SaveDraftAsync(draft.Id, request);
        var resumed = await db.Service().StartDraftAsync(new(2026,11,1));
        Assert.Equal(draft.Id, resumed.Id);
        Assert.Equal(saved.EditVersion, resumed.EditVersion);
        Assert.Equal(BudgetSetupStep.Bills, resumed.SetupStep);
        Assert.Equal(new DateOnly(2026,10,1), resumed.EffectiveFromMonth);
        Assert.Single(await db.Context().HouseholdBudgets.ToListAsync());
        var month = await db.Service().GetMonthAsync(new(2026,10,1));
        Assert.Null(month.RevisionId);
        Assert.Equal(0, month.ExpectedIncome);
    }

    [Fact]
    public async Task PublicationProjectsRealPaydaysAndScheduledAllocationsWithoutLedgerWrites()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var published = await Publish(db.Service());
        Assert.Equal(BudgetRevisionState.Published, published.State);
        Assert.Equal(db.Clock.Now.UtcDateTime, published.PublishedAt);
        Assert.Null(await db.Service().GetDraftAsync());
        var october = await db.Service().GetMonthAsync(new(2026,10,1));
        Assert.Equal(6000, october.ExpectedIncome);
        Assert.Equal(2700, october.Allocated);
        Assert.Equal(3300, october.Unassigned);
        Assert.Equal(0, october.OverBudget);
        Assert.Equal(10, october.Events.Count);
        Assert.DoesNotContain(october.Events, e => e.Name == "Groceries");
        Assert.Equal(3, Assert.Single(october.IncomeSources).OccurrenceCount);
        Assert.Equal("Salary", Assert.Single(published.IncomeSources).Name);
        var november = await db.Service().GetMonthAsync(new(2026,11,1));
        Assert.Equal(4000, november.ExpectedIncome);
        Assert.Contains(november.Events, e => e.Name == "Investing" && e.Date == new DateOnly(2026,11,30));
        Assert.Empty(await db.Context().Transactions.ToListAsync());
    }

    [Fact]
    public async Task OverAllocationIsAllowedAndReportedSeparatelyFromUnassigned()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var service = db.Service();
        var draft = await service.StartDraftAsync();
        var request = Request();
        request.Allocations = [new() { Name = "Spending", Amount = 7000, AllocationType = BudgetAllocationType.Spending }];
        var saved = await service.SaveDraftAsync(draft.Id, request);
        await service.PublishDraftAsync(saved.Id, saved.EditVersion);
        var month = await service.GetMonthAsync(new(2026,10,1));
        Assert.Equal(1000, month.OverBudget);
        Assert.Equal(0, month.Unassigned);
    }

    [Fact]
    public async Task StaleSavesAndPublicationCannotOverwriteAnotherMembersDraft()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var original = await db.Service().StartDraftAsync();
        var otherCopy = await db.Service().GetDraftAsync();
        var saved = await db.Service().SaveDraftAsync(original.Id, Request(original.EditVersion));
        Assert.NotEqual(otherCopy!.EditVersion, saved.EditVersion);
        await Assert.ThrowsAsync<BudgetConflictException>(() => db.Service().SaveDraftAsync(original.Id, Request(otherCopy.EditVersion)));
        await Assert.ThrowsAsync<BudgetConflictException>(() => db.Service().PublishDraftAsync(original.Id, otherCopy.EditVersion));
        var latest = await db.Service().GetDraftAsync();
        Assert.Equal(saved.EditVersion, latest!.EditVersion);
        Assert.Equal(2000, Assert.Single(latest.IncomeSources).Amount);
    }

    [Fact]
    public async Task OtherHouseholdsCannotReadOrChangeDraftsAndPublishedRowsCannotBeEdited()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var draft = await db.Service().StartDraftAsync();
        Assert.Null(await db.Service(2).GetDraftAsync());
        await Assert.ThrowsAsync<BudgetDraftNotFoundException>(() => db.Service(2).SaveDraftAsync(draft.Id, Request()));
        await Assert.ThrowsAsync<BudgetDraftNotFoundException>(() => db.Service(2).PublishDraftAsync(draft.Id, 0));
        var published = await Publish(db.Service());
        await Assert.ThrowsAsync<BudgetDraftNotFoundException>(() => db.Service().SaveDraftAsync(published.Id, Request(published.EditVersion)));
        await Assert.ThrowsAsync<BudgetDraftNotFoundException>(() => db.Service().PublishDraftAsync(published.Id, published.EditVersion));
        Assert.Null((await db.Service(2).GetMonthAsync(new(2026,10,1))).RevisionId);
    }

    [Fact]
    public async Task RevisionsPreserveHistoryAndAlreadyScheduledFutureChanges()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var original = await Publish(db.Service());
        var future = await db.Service().StartDraftAsync(new(2026,11,1));
        Assert.Equal(2000, Assert.Single(future.IncomeSources).Amount);
        var futureRequest = Request(future.EditVersion, new(2026,11,1));
        futureRequest.IncomeSources[0].Amount = 3000;
        future = await db.Service().SaveDraftAsync(future.Id, futureRequest);
        await db.Service().PublishDraftAsync(future.Id, future.EditVersion);
        var replacement = await db.Service().StartDraftAsync(new(2026,10,1));
        var request = Request(replacement.EditVersion);
        request.IncomeSources[0].Amount = 2500;
        replacement = await db.Service().SaveDraftAsync(replacement.Id, request);
        await db.Service().PublishDraftAsync(replacement.Id, replacement.EditVersion);
        Assert.Equal(7500, (await db.Service().GetMonthAsync(new(2026,10,1))).ExpectedIncome);
        Assert.Equal(6000, (await db.Service().GetMonthAsync(new(2026,11,1))).ExpectedIncome);
        Assert.Null((await db.Service().GetMonthAsync(new(2026,9,1))).RevisionId);
        var stored = await db.Context().BudgetIncomeSources.SingleAsync(i => i.BudgetRevisionId == original.Id);
        Assert.Equal(2000, stored.Amount);
    }

    [Fact]
    public async Task NewYorkMonthBoundaryAndStaleDraftRolloverAreHandled()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        db.Clock.Now = new(2026,11,1,0,30,0,TimeSpan.Zero);
        var draft = await db.Service().StartDraftAsync();
        Assert.Equal(new DateOnly(2026,10,1), draft.EffectiveFromMonth);
        var saved = await db.Service().SaveDraftAsync(draft.Id, Request());
        db.Clock.Now = new(2026,11,1,6,0,0,TimeSpan.Zero);
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().PublishDraftAsync(saved.Id, saved.EditVersion));
        saved = await db.Service().SaveDraftAsync(saved.Id, Request(saved.EditVersion, new(2026,11,1)));
        Assert.Equal(BudgetRevisionState.Published, (await db.Service().PublishDraftAsync(saved.Id, saved.EditVersion)).State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task InvalidDraftPayloadDoesNotChangeStoredData(int scenario)
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var draft = await db.Service().StartDraftAsync();
        var request = Request();
        switch (scenario) {
            case 0: request.IncomeSources[0].Amount = -.01m; break;
            case 1: request.Allocations[0].Amount = .001m; break;
            case 2: request.Allocations[0].Name = " "; break;
            case 3: request.EffectiveFromMonth = new(2026,10,2); break;
            case 4: request.IncomeSources[0].Schedule.Frequency = (BudgetFrequency)99; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().SaveDraftAsync(draft.Id, request));
        var current = await db.Service().GetDraftAsync();
        Assert.Equal(0, current!.EditVersion);
        Assert.Empty(current.IncomeSources);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task IncompleteDraftsCannotBePublished(int scenario)
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var draft = await db.Service().StartDraftAsync();
        var request = Request();
        switch (scenario) {
            case 0: request.SetupStep = BudgetSetupStep.Spending; break;
            case 1: request.IncomeSources.Clear(); break;
            case 2: request.Allocations[0].Schedule = null; break;
        }
        var saved = await db.Service().SaveDraftAsync(draft.Id, request);
        await Assert.ThrowsAsync<ArgumentException>(() => db.Service().PublishDraftAsync(saved.Id, saved.EditVersion));
        Assert.Equal(BudgetRevisionState.Draft, (await db.Service().GetDraftAsync())!.State);
    }

    [Fact]
    public async Task FailedChildWriteRollsBackVersionAndAllReplacedItems()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var draft = await db.Service().StartDraftAsync();
        var saved = await db.Service().SaveDraftAsync(draft.Id, Request());
        await db.Context().Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectBudgetAllocation BEFORE INSERT ON BudgetAllocations
            BEGIN SELECT RAISE(ABORT, 'Simulated storage failure'); END;
            """);
        var update = Request(saved.EditVersion); update.IncomeSources[0].Amount = 9999;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.Service().SaveDraftAsync(saved.Id, update));
        var restored = await db.Service().GetDraftAsync();
        Assert.Equal(saved.EditVersion, restored!.EditVersion);
        Assert.Equal(2000, Assert.Single(restored.IncomeSources).Amount);
        Assert.Equal(4, restored.Allocations.Count);
    }
}
