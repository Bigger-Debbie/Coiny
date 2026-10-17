using System.Collections.Concurrent;
using Coiny.API.Data;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Tests;

internal sealed class InvitationTestDatabase : IAsyncDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "coiny-invitations-" + Guid.NewGuid() + ".db");
    private readonly ConcurrentBag<CoinyContext> contexts = [];
    public readonly TestClock Clock = new();
    public CoinyContext Context()
    {
        var context = new CoinyContext(new DbContextOptionsBuilder<CoinyContext>().UseSqlite("Data Source=" + path + ";Pooling=False").Options);
        contexts.Add(context); return context;
    }
    public HouseholdInvitationService Service(string user = "owner") => new(Context(), new InvitationUser(user), Clock);
    public HouseholdService Households(string user) => new(Context(), new InvitationUser(user), Clock);
    public static async Task<InvitationTestDatabase> Create()
    {
        var db = new InvitationTestDatabase(); var context = db.Context(); await context.Database.MigrateAsync();
        foreach (var id in new[] { "owner", "owner2", "member", "new1", "new2", "new3" })
            context.Users.Add(new ApplicationUser { Id = id, UserName = id + "@coiny.test", Email = id + "@coiny.test" });
        context.Households.AddRange(new Household { Id = 1, Name = "Ross household" }, new Household { Id = 2, Name = "Other household" });
        context.HouseholdMembers.AddRange(new HouseholdMember { HouseholdId = 1, UserId = "owner", Role = HouseholdRole.Owner },
            new HouseholdMember { HouseholdId = 1, UserId = "member", Role = HouseholdRole.Member },
            new HouseholdMember { HouseholdId = 2, UserId = "owner2", Role = HouseholdRole.Owner });
        await context.SaveChangesAsync(); return db;
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var context in contexts) await context.DisposeAsync();
        foreach (var file in new[] { path, path + "-wal", path + "-shm" }) if (File.Exists(file)) File.Delete(file);
    }
}
internal sealed class InvitationUser(string id) : ICurrentUserService
{
    public string? GetUserId() => id;
    public bool IsAuthenticated() => true;
    public Task<int> GetCurrentHouseholdIdAsync() => throw new NotSupportedException();
}
