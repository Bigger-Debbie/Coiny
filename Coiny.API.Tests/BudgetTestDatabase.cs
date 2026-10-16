using Coiny.API.Data;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

internal sealed class BudgetTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly List<CoinyContext> contexts = [];
    public readonly TestClock Clock = new();
    public static async Task<BudgetTestDatabase> CreateAsync()
    {
        var database = new BudgetTestDatabase();
        await database.connection.OpenAsync();
        var context = database.Context();
        await context.Database.MigrateAsync();
        context.Households.AddRange(new Household { Id = 1, Name = "Home one" }, new Household { Id = 2, Name = "Home two" });
        await context.SaveChangesAsync();
        return database;
    }
    public CoinyContext Context()
    {
        var context = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite(connection).Options);
        contexts.Add(context);
        return context;
    }
    public BudgetService Service(int householdId = 1) => new(Context(), new CurrentUser(householdId), new BudgetScheduleService(), Clock);
    public async ValueTask DisposeAsync()
    {
        foreach (var context in contexts) await context.DisposeAsync();
        await connection.DisposeAsync();
    }
    private sealed class CurrentUser(int householdId) : ICurrentUserService
    {
        public string? GetUserId() => "test";
        public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(householdId);
    }
}
internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026,10,16,16,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
