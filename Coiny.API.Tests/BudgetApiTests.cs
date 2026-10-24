using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coiny.API.Controllers;
using Coiny.API.DTOs.Budgets;
using Coiny.API.Interfaces;
using Coiny.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coiny.API.Tests;

public class BudgetApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter() },
    };
    private const string CompleteDraft = """
        {"expectedEditVersion":0,"effectiveFromMonth":"2026-11-01","setupStep":"Review",
         "incomeSources":[{"name":"Salary","amount":2000,"schedule":{"frequency":"EveryTwoWeeks","startDate":"2026-10-02"}}],
         "allocations":[{"name":"Groceries","amount":600,"allocationType":"Spending"}]}
        """;
    private static StringContent Body(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Forecast_http_route_supports_long_ranges_validates_horizons_and_requires_authentication()
    {
        await using var host=await Api.Create(); var service=host.Database.Service();
        var draft=await service.StartDraftAsync(new(2026,11,1));
        var saved=await service.SaveDraftAsync(draft.Id,JsonSerializer.Deserialize<BudgetDraftRequest>(CompleteDraft,Json)!);
        await service.PublishDraftAsync(saved.Id,saved.EditVersion);
        var forecast=await host.Client.GetFromJsonAsync<BudgetForecastResponse>("/api/budgets/forecast?asOf=2026-11-13&months=6",Json);
        Assert.True(forecast!.Periods.Count>5); Assert.True(forecast.EndDate>new DateOnly(2027,5,1));
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.GetAsync("/api/budgets/forecast?months=2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.GetAsync("/api/budgets/forecast?asOf=bad")).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Test-Household");
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.GetAsync("/api/budgets/forecast")).StatusCode);
    }
    [Fact]
    public async Task Published_item_management_reviews_then_publishes_and_rejects_stale_saves()
    {
        await using var host = await Api.Create();
        var service = host.Database.Service(); var draft = await service.StartDraftAsync(new(2026,11,1));
        var saved = await service.SaveDraftAsync(draft.Id, JsonSerializer.Deserialize<BudgetDraftRequest>(CompleteDraft,Json)!);
        await service.PublishDraftAsync(saved.Id,saved.EditVersion);
        var management = (await host.Client.GetFromJsonAsync<BudgetManagementResponse>("/api/budgets/management",Json))!;
        var change = new BudgetItemChange { ExpectedRevisionId = management.Revision.Id, EffectiveDate = new(2026,11,15),
            SourceKey = management.Revision.Allocations[0].SourceKey, Allocation = new() { Name = "Groceries", Amount = 900, AllocationType = Coiny.API.Enums.BudgetAllocationType.Spending } };
        (await host.Client.PostAsJsonAsync("/api/budgets/items/preview",change,Json)).EnsureSuccessStatusCode();
        Assert.Equal(management.Revision.Id,(await service.GetManagementAsync()).Revision.Id);
        (await host.Client.PostAsJsonAsync("/api/budgets/items",change,Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict,(await host.Client.PostAsJsonAsync("/api/budgets/items",change,Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.PostAsync("/api/budgets/items",Body("{}"))).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Test-Household");
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.GetAsync("/api/budgets/management")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.PostAsJsonAsync("/api/budgets/items",change,Json)).StatusCode);
    }

    [Fact]
    public async Task HttpWorkflowSupportsResumePreviewPublishAndFutureSetupStatus()
    {
        await using var host = await Api.Create();
        var client = host.Client;
        var setup = await client.GetFromJsonAsync<BudgetSetupStatusResponse>("/api/budgets/setup", Json);
        Assert.False(setup!.HasPublishedBudget);
        Assert.Null(setup.Draft);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/budgets/draft")).StatusCode);
        var draft = await (await client.PostAsync("/api/budgets/draft", Body("{\"effectiveFromMonth\":\"2026-11-01\"}")))
            .Content.ReadFromJsonAsync<BudgetRevisionResponse>(Json);
        var path = $"/api/budgets/draft/{draft!.Id}";
        var savedResponse = await client.PutAsync(path, Body(CompleteDraft));
        savedResponse.EnsureSuccessStatusCode();
        var saved = (await savedResponse.Content.ReadFromJsonAsync<BudgetRevisionResponse>(Json))!;
        Assert.Equal(1, saved.EditVersion);
        var resumed = await (await client.PostAsync("/api/budgets/draft", Body("{}")))
            .Content.ReadFromJsonAsync<BudgetRevisionResponse>(Json);
        Assert.Equal(saved.Id, resumed!.Id);
        Assert.Equal(1, resumed.EditVersion);
        var preview = await client.GetFromJsonAsync<BudgetDraftPreviewResponse>(path + "/preview?month=2026-11-01&expectedEditVersion=1", Json);
        Assert.Equal(4000, preview!.Month.ExpectedIncome);
        Assert.Equal(3400, preview.Month.Unassigned);
        Assert.Null((await client.GetFromJsonAsync<BudgetMonthResponse>("/api/budgets/month?month=2026-11-01", Json))!.RevisionId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsync(path, Body(CompleteDraft))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path + "/publish", Body("{\"expectedEditVersion\":0}"))).StatusCode);
        (await client.PostAsync(path + "/publish", Body("{\"expectedEditVersion\":1}"))).EnsureSuccessStatusCode();
        setup = await client.GetFromJsonAsync<BudgetSetupStatusResponse>("/api/budgets/setup", Json);
        Assert.True(setup!.HasPublishedBudget);
        Assert.Equal(new DateOnly(2026,11,1), setup.FirstPublishedMonth);
        Assert.Null(setup.CurrentRevisionId);
        Assert.Null(setup.Draft);
        Assert.Equal(4000, (await client.GetFromJsonAsync<BudgetMonthResponse>("/api/budgets/month?month=2026-11-01", Json))!.ExpectedIncome);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync(path, Body(CompleteDraft))).StatusCode);
    }

    [Theory]
    [InlineData("/api/budgets/month")]
    [InlineData("/api/budgets/month?month=bad")]
    [InlineData("/api/budgets/month?month=2026-10-02")]
    [InlineData("/api/budgets/draft/1/preview?month=2026-11-01")]
    [InlineData("/api/budgets/draft/1/preview?month=2026-11-01&expectedEditVersion=-1")]
    public async Task InvalidQueriesReturnBadRequest(string url)
    {
        await using var host = await Api.Create();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("expectedEditVersion")]
    [InlineData("allocations")]
    [InlineData("incomeSources")]
    [InlineData("setupStep")]
    public async Task MissingFieldsCannotSilentlyEraseDraft(string field)
    {
        await using var host = await Api.Create();
        var draft = await host.Database.Service().StartDraftAsync(new(2026,11,1));
        var json = System.Text.Json.Nodes.JsonNode.Parse(CompleteDraft)!;
        json.AsObject().Remove(field);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync($"/api/budgets/draft/{draft.Id}", Body(json.ToJsonString()))).StatusCode);
        Assert.Equal(0, (await host.Database.Service().GetDraftAsync())!.EditVersion);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsync($"/api/budgets/draft/{draft.Id}/publish", Body("{}"))).StatusCode);
    }

    [Fact]
    public async Task AuthenticationAndHouseholdIsolationAreEnforced()
    {
        await using var host = await Api.Create();
        var draft = await host.Database.Service().StartDraftAsync(new(2026,11,1));
        host.Client.DefaultRequestHeaders.Remove("Test-Household");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/budgets/setup")).StatusCode);
        host.Client.DefaultRequestHeaders.Add("Test-Household", "2");
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PutAsync($"/api/budgets/draft/{draft.Id}", Body(CompleteDraft))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/budgets/draft/{draft.Id}/preview?month=2026-11-01&expectedEditVersion=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PostAsync($"/api/budgets/draft/{draft.Id}/publish", Body("{\"expectedEditVersion\":0}"))).StatusCode);
        Assert.Null((await host.Client.GetFromJsonAsync<BudgetSetupStatusResponse>("/api/budgets/setup", Json))!.Draft);
        host.Client.DefaultRequestHeaders.Remove("Test-Household");
        host.Client.DefaultRequestHeaders.Add("Test-Household", "none");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/budgets/setup")).StatusCode);
    }

    [Fact]
    public async Task IncomePeriodEndpointValidatesQueriesAndKeepsHouseholdScope()
    {
        await using var host = await Api.Create();
        var draft = await host.Database.Service().StartDraftAsync();
        draft = await host.Database.Service().SaveDraftAsync(draft.Id, new() {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = draft.EffectiveFromMonth, SetupStep = Coiny.API.Enums.BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 1000, Schedule = new() {
                Frequency = Coiny.API.Enums.BudgetFrequency.Weekly, StartDate = new(2026,10,2) } }],
        });
        await host.Database.Service().PublishDraftAsync(draft.Id, draft.EditVersion);
        var period = await host.Client.GetFromJsonAsync<BudgetIncomePeriodResponse>("/api/budgets/income-period", Json);
        Assert.Equal(new DateOnly(2026,10,16), period!.StartDate);
        var key = Assert.Single(period.Sources).SourceKey;
        (await host.Client.GetAsync($"/api/budgets/income-period?asOf=2026-11-01&sourceKey={key}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/budgets/income-period?sourceKey=bad")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/budgets/income-period?asOf=0001-01-01")).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Test-Household"); host.Client.DefaultRequestHeaders.Add("Test-Household", "2");
        Assert.Empty((await host.Client.GetFromJsonAsync<BudgetIncomePeriodResponse>("/api/budgets/income-period", Json))!.Sources);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync($"/api/budgets/income-period?sourceKey={key}")).StatusCode);
    }

    [Fact]
    public async Task BillPaymentHttpRoutesValidateBindingMembershipAndOccurrence()
    {
        await using var host = await Api.Create();
        var draft = await host.Database.Service().StartDraftAsync();
        var saved = await host.Database.Service().SaveDraftAsync(draft.Id, new() {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026,10,1), SetupStep = Coiny.API.Enums.BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 1000, Schedule = new() { Frequency = Coiny.API.Enums.BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 1 } }],
            Allocations = [new() { Name = "Electric", Amount = 100, AllocationType = Coiny.API.Enums.BudgetAllocationType.Bills, Schedule = new() { Frequency = Coiny.API.Enums.BudgetFrequency.Monthly, StartDate = new(2026,10,1), DayOfMonth = 17 } }],
        });
        await host.Database.Service().PublishDraftAsync(saved.Id, saved.EditVersion);
        var path = $"/api/budgets/bills/{saved.Allocations[0].SourceKey}/2026-10-17";
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(path + "/payment", Body("{}"))).StatusCode);
        var result = await host.Client.PutAsync(path + "/payment", Body("{\"isPaid\":true,\"paidOn\":\"2026-10-15\"}"));
        result.EnsureSuccessStatusCode(); Assert.True((await result.Content.ReadFromJsonAsync<BillPaymentResponse>())!.IsPaid);
        Assert.Empty((await host.Client.GetFromJsonAsync<BillTransactionCandidate[]>(path + "/transactions"))!);
        host.Client.DefaultRequestHeaders.Remove("Test-Household");
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(path + "/transactions")).StatusCode);
        host.Client.DefaultRequestHeaders.Add("Test-Household", "2");
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(path + "/payment", Body("{\"isPaid\":false}"))).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Test-Household"); host.Client.DefaultRequestHeaders.Add("Test-Household", "none");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(path + "/transactions")).StatusCode);
    }

    [Fact]
    public async Task ContributionPaymentHttpAcceptsTransferAndProjectsStatusIntoMonthAndPeriod()
    {
        await using var host = await Api.Create(); var draft = await host.Database.Service().StartDraftAsync();
        var saved = await host.Database.Service().SaveDraftAsync(draft.Id, new()
        {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026, 10, 1), SetupStep = Coiny.API.Enums.BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 1000, Schedule = new() { Frequency = Coiny.API.Enums.BudgetFrequency.Monthly, StartDate = new(2026, 10, 1), DayOfMonth = 1 } }],
            Allocations = [new() { Name = "IRA", Amount = 100, AllocationType = Coiny.API.Enums.BudgetAllocationType.Investments, Schedule = new() { Frequency = Coiny.API.Enums.BudgetFrequency.Monthly, StartDate = new(2026, 10, 1), DayOfMonth = 17 } }]
        });
        await host.Database.Service().PublishDraftAsync(saved.Id, saved.EditVersion);
        var c = host.Database.Context();
        c.Accounts.Add(new() { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalanceDate = new(2026, 10, 1) });
        c.Categories.Add(new() { Id = 1, HouseholdId = 1, Name = "Transfer", CategoryType = Coiny.API.Enums.CategoryType.Transfer, IsActive = true, IsSystem = true });
        c.Transactions.Add(new() { Id = 42, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 100, Description = "IRA transfer", TransactionDate = new(2026, 10, 15), TransferDirection = Coiny.API.Enums.TransferDirection.Out, TransferCounterparty = Coiny.API.Enums.TransferCounterparty.External });
        await c.SaveChangesAsync();
        var path = $"/api/budgets/bills/{saved.Allocations[0].SourceKey}/2026-10-17";
        var candidates = await host.Client.GetFromJsonAsync<BillTransactionCandidate[]>(path + "/transactions", Json);
        Assert.Equal(Coiny.API.Enums.CategoryType.Transfer, Assert.Single(candidates!).CategoryType);
        var result = await host.Client.PutAsync(path + "/payment", Body("{\"isPaid\":true,\"transactionId\":42}")); result.EnsureSuccessStatusCode();
        Assert.Equal(42, (await result.Content.ReadFromJsonAsync<BillPaymentResponse>(Json))!.TransactionId);
        var month = await host.Client.GetFromJsonAsync<BudgetMonthResponse>("/api/budgets/month?month=2026-10-01", Json);
        Assert.True(Assert.Single(month!.Events, e => !e.IsIncome).Payment!.IsPaid);
        var period = await host.Client.GetFromJsonAsync<BudgetIncomePeriodResponse>("/api/budgets/income-period", Json);
        Assert.Equal(0, period!.UnpaidContributionsTotal); Assert.Equal(100, period.ContributionsTotal);
        Assert.Single(await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(host.Database.Context().Transactions));
        host.Client.DefaultRequestHeaders.Remove("Test-Household"); host.Client.DefaultRequestHeaders.Add("Test-Household", "2");
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync(path + "/payment", Body("{\"isPaid\":true,\"transactionId\":42}"))).StatusCode);
    }

    [Fact]
    public async Task ActualsHttpRequiresMembershipBindingAndReviewedVersions()
    {
        await using var host = await Api.Create();
        var draft = await host.Database.Service().StartDraftAsync();
        var saved = await host.Database.Service().SaveDraftAsync(draft.Id, new()
        {
            ExpectedEditVersion = draft.EditVersion, EffectiveFromMonth = new(2026, 10, 1), SetupStep = Coiny.API.Enums.BudgetSetupStep.Review,
            IncomeSources = [new() { Name = "Salary", Amount = 1000, Schedule = new() { Frequency = Coiny.API.Enums.BudgetFrequency.Monthly, StartDate = new(2026, 10, 1), DayOfMonth = 1 } }],
            Allocations = [new() { Name = "Groceries", Amount = 300, AllocationType = Coiny.API.Enums.BudgetAllocationType.Spending }]
        });
        await host.Database.Service().PublishDraftAsync(saved.Id, saved.EditVersion);
        var c = host.Database.Context(); c.Accounts.Add(new() { Id = 1, HouseholdId = 1, Name = "Checking", OpeningBalanceDate = new(2026, 10, 1) });
        c.Categories.Add(new() { Id = 1, HouseholdId = 1, Name = "Food", CategoryType = Coiny.API.Enums.CategoryType.Expense, IsActive = true });
        c.Transactions.Add(new() { Id = 1, HouseholdId = 1, AccountId = 1, CategoryId = 1, Amount = 40, Description = "Market", TransactionDate = new(2026, 10, 15) }); await c.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/budgets/actuals")).StatusCode);
        var report = await host.Client.GetFromJsonAsync<BudgetActualsResponse>("/api/budgets/actuals?month=2026-10-01", Json); Assert.Equal(40, report!.UnassignedExpenses);
        var periodReport = await host.Client.GetFromJsonAsync<BudgetActualsResponse>("/api/budgets/actuals/period?asOf=2026-10-16",Json);
        Assert.Equal(new DateOnly(2026,10,1),periodReport!.StartDate); Assert.Equal(new DateOnly(2026,10,31),periodReport.EndDate);
        Assert.Equal(300,periodReport.Planned); Assert.Equal(40,periodReport.UnassignedExpenses);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.GetAsync("/api/budgets/actuals/period?asOf=invalid")).StatusCode);
        var category = $"{{\"month\":\"2026-10-01\",\"allocationKey\":\"{saved.Allocations[0].SourceKey}\",\"expectedEditVersion\":0}}";
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PutAsync("/api/budgets/actuals/categories/1", Body(category))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsync("/api/budgets/actuals/categories/1", Body(category))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsync("/api/budgets/actuals/transactions/1", Body("{\"month\":\"2026-10-01\",\"useCategoryDefault\":false}"))).StatusCode);
        var assignment = "{\"month\":\"2026-10-01\",\"useCategoryDefault\":false,\"allocationKey\":null,\"expectedEditVersion\":0}";
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PutAsync("/api/budgets/actuals/transactions/1", Body(assignment))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsync("/api/budgets/actuals/transactions/1", Body(assignment))).StatusCode);
        host.Client.DefaultRequestHeaders.Remove("Test-Household"); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/budgets/actuals?month=2026-10-01")).StatusCode);
        host.Client.DefaultRequestHeaders.Add("Test-Household", "none"); Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/budgets/actuals?month=2026-10-01")).StatusCode);
    }

    private sealed class Api(BudgetTestDatabase database, WebApplication app) : IAsyncDisposable
    {
        public BudgetTestDatabase Database => database;
        public HttpClient Client { get; } = app.GetTestClient();
        public static async Task<Api> Create()
        {
            var db = await BudgetTestDatabase.CreateAsync();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddControllers().AddApplicationPart(typeof(BudgetsController).Assembly)
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuth>("Test", _ => {});
            builder.Services.AddAuthorization();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IBudgetService>(services => {
                var household = services.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext!
                    .User.FindFirstValue("household")!;
                return household == "none" ? new BudgetService(db.Context(), new MissingHousehold(), new BudgetScheduleService(), db.Clock)
                    : db.Service(int.Parse(household));
            });
            builder.Services.AddScoped<BillPaymentService>(services => {
                var household = services.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext!.User.FindFirstValue("household")!;
                ICurrentUserService user = household == "none" ? new MissingHousehold() : new HouseholdUser(int.Parse(household));
                return new(db.Context(), user, services.GetRequiredService<IBudgetService>(), db.Clock);
            });
            builder.Services.AddScoped<BudgetActualsService>(services => {
                var household = services.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext!.User.FindFirstValue("household")!;
                ICurrentUserService user = household == "none" ? new MissingHousehold() : new HouseholdUser(int.Parse(household));
                return new(db.Context(), user, services.GetRequiredService<IBudgetService>(), db.Clock);
            });
            var app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            await app.StartAsync();
            var api = new Api(db, app);
            api.Client.DefaultRequestHeaders.Add("Test-Household", "1");
            return api;
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await app.DisposeAsync(); await database.DisposeAsync();
        }
    }
    private sealed class HouseholdUser(int household) : ICurrentUserService
    {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    private sealed class MissingHousehold : ICurrentUserService
    {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => throw new HouseholdMembershipRequiredException();
    }
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var household = Request.Headers["Test-Household"].ToString();
            return Task.FromResult(string.IsNullOrEmpty(household) ? AuthenticateResult.NoResult() :
                AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim("household", household)], "Test")), "Test")));
        }
    }
}
