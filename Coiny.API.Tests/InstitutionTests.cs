using Coiny.API.DTOs.Institutions;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Coiny.API.Services;
using Microsoft.EntityFrameworkCore;
namespace Coiny.API.Tests;

public class InstitutionTests
{
    private sealed class User(int household = 1) : ICurrentUserService {
        public string? GetUserId() => "test"; public bool IsAuthenticated() => true;
        public Task<int> GetCurrentHouseholdIdAsync() => Task.FromResult(household);
    }
    private static InstitutionService Service(BudgetTestDatabase db, int household = 1) => new(db.Context(), new User(household));
    [Fact] public async Task Renaming_updates_linked_accounts_and_transactions_without_changing_money_or_links()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var institution = await Service(db).CreateInstitutionAsync(new() { Name = "Old bank", Website = "https://old.example" });
        var c = db.Context(); c.Accounts.Add(new() { Id = 1, HouseholdId = 1, InstitutionId = institution.Id, Name = "Checking", OpeningBalance = 100, OpeningBalanceDate = new(2026,10,1) });
        c.Categories.Add(new() { Id = 1, HouseholdId = 1, Name = "Food", CategoryType = Coiny.API.Enums.CategoryType.Expense });
        c.Transactions.Add(new() { AccountId = 1, HouseholdId = 1, CategoryId = 1, Amount = 10, Description = "Lunch", TransactionDate = new(2026,10,16) }); await c.SaveChangesAsync();
        var request = new UpdateInstitutionRequest { Name = " New bank ", Website = " https://new.example ", ExpectedName = institution.Name, ExpectedWebsite = institution.Website };
        var saved = await Service(db).UpdateInstitutionAsync(institution.Id, request);
        Assert.Equal("New bank", saved.Name); Assert.Equal("https://new.example", saved.Website);
        var fresh = db.Context(); var account = await fresh.Accounts.AsNoTracking().Include(a => a.Institution).SingleAsync();
        Assert.Equal(institution.Id, account.InstitutionId); Assert.Equal("New bank", account.Institution!.Name);
        Assert.Equal(90, await new AccountBalanceService(fresh).GetCurrentBalanceAsync(1)); Assert.Single(await fresh.Transactions.ToListAsync());
        var row = await fresh.Transactions.AsNoTracking().Include(t => t.Account).ThenInclude(a => a.Institution).SingleAsync(); Assert.Equal("New bank", row.Account.Institution!.Name);
        await Assert.ThrowsAsync<InstitutionConflictException>(() => Service(db).UpdateInstitutionAsync(institution.Id, request));
        saved = await Service(db).UpdateInstitutionAsync(institution.Id, new() { Name=saved.Name, Website=null, ExpectedName=saved.Name, ExpectedWebsite=saved.Website }); Assert.Null(saved.Website);
    }
    [Fact] public async Task Updates_reject_foreign_ids_blank_names_and_unsafe_websites()
    {
        await using var db = await BudgetTestDatabase.CreateAsync();
        var institution = await Service(db).CreateInstitutionAsync(new() { Name="Bank", Website=null });
        var r = new UpdateInstitutionRequest { Name="Renamed", ExpectedName=institution.Name, ExpectedWebsite=null };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db,2).UpdateInstitutionAsync(institution.Id,r));
        r.Name=" "; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UpdateInstitutionAsync(institution.Id,r));
        r.Name="Renamed";
        foreach(var website in new[]{"javascript:alert(1)","file:///etc/passwd","https://user:password@example.test","not a url"}) {
            r.Website=website; await Assert.ThrowsAsync<ArgumentException>(() => Service(db).UpdateInstitutionAsync(institution.Id,r));
        }
        Assert.Equal("Bank", Assert.Single(await Service(db).GetInstitutionsAsync()).Name); Assert.Empty(await Service(db,2).GetInstitutionsAsync());
    }
}
