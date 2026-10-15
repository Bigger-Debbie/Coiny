using System.Security.Claims;
using Coiny.API.DTOs.Transactions;
using Coiny.API.Enums;
using Coiny.API.Interfaces;
using Coiny.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data;

/// <summary>Explicit development command; never runs during normal API startup.</summary>
public class DemoDataSeeder(
    CoinyContext context,
    UserManager<ApplicationUser> users,
    IHttpContextAccessor httpContextAccessor,
    IHouseholdService households,
    IInstitutionService institutions,
    ICategoryService categories,
    IAccountService accounts,
    ITransactionService transactions,
    TimeProvider timeProvider,
    IHostEnvironment environment)
{
    public const string Email = "alex@coiny.test";
    public const string Password = "CoinyDemo!2026";

    public async Task SeedAsync()
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Demo seeding is only available in Development.");

        await context.Database.MigrateAsync();

        if (await context.Users.AnyAsync() || await context.Households.AnyAsync() ||
            await context.Institutions.AnyAsync() || await context.Accounts.AnyAsync() ||
            await context.Categories.AnyAsync() || await context.Transactions.AnyAsync() ||
            await context.AccountBalanceSnapshots.AnyAsync())
            throw new InvalidOperationException("Demo seeding requires an empty database. Existing data has not been changed.");

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime);
        var openingDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-6);
        var user = new ApplicationUser { UserName = Email, Email = Email, FirstName = "Alex" };
        var created = await users.CreateAsync(user, Password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(error => error.Description)));

        var previousContext = httpContextAccessor.HttpContext;
        try
        {
            // Exercise the same household-scoped services used by authenticated API calls.
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, user.Id)], "DevelopmentSeed"))
            };

            await households.CreateHouseholdAsync(new() { Name = "Ross" });
            var bank = await institutions.CreateInstitutionAsync(new() { Name = "Demo Community Bank" });
            var brokerage = await institutions.CreateInstitutionAsync(new() { Name = "Demo Retirement" });
            var cardProvider = await institutions.CreateInstitutionAsync(new() { Name = "Demo Credit Union" });

            var checking = await accounts.CreateAccountAsync(new()
            {
                Name = "Primary Checking", AccountType = AccountType.Checking,
                InstitutionId = bank.Id, OpeningBalance = 6000m, OpeningBalanceDate = openingDate
            });
            var savings = await accounts.CreateAccountAsync(new()
            {
                Name = "Emergency Savings", AccountType = AccountType.Savings,
                InstitutionId = bank.Id, OpeningBalance = 12000m, OpeningBalanceDate = openingDate
            });
            var retirement = await accounts.CreateAccountAsync(new()
            {
                Name = "401(k)", AccountType = AccountType.Retirement,
                InstitutionId = brokerage.Id, OpeningBalance = 75000m, OpeningBalanceDate = openingDate
            });
            var card = await accounts.CreateAccountAsync(new()
            {
                Name = "Everyday Credit Card", AccountType = AccountType.CreditCard,
                InstitutionId = cardProvider.Id, OpeningBalance = 1800m, OpeningBalanceDate = openingDate
            });

            var salary = await categories.CreateCategoryAsync(new() { Name = "Salary", CategoryType = CategoryType.Income });
            var interest = await categories.CreateCategoryAsync(new() { Name = "Interest", CategoryType = CategoryType.Income });
            var contributions = await categories.CreateCategoryAsync(new() { Name = "Employer Contributions", CategoryType = CategoryType.Income });
            var housing = await categories.CreateCategoryAsync(new() { Name = "Housing", CategoryType = CategoryType.Expense });
            var utilities = await categories.CreateCategoryAsync(new() { Name = "Utilities", CategoryType = CategoryType.Expense });
            var groceries = await categories.CreateCategoryAsync(new() { Name = "Groceries", CategoryType = CategoryType.Expense });
            var dining = await categories.CreateCategoryAsync(new() { Name = "Dining", CategoryType = CategoryType.Expense });
            var transport = await categories.CreateCategoryAsync(new() { Name = "Transportation", CategoryType = CategoryType.Expense });
            var fees = await categories.CreateCategoryAsync(new() { Name = "Investment Fees", CategoryType = CategoryType.Expense });

            async Task Add(int accountId, int categoryId, DateOnly date, decimal amount, string description, string merchant)
            {
                await transactions.CreateTransactionAsync(new CreateTransactionRequest
                {
                    AccountId = accountId, CategoryId = categoryId, Amount = amount,
                    TransactionDate = date.ToDateTime(new TimeOnly(12, 0)),
                    Description = description, Merchant = merchant,
                    Notes = "Synthetic development sample", IsCleared = true
                });
            }

            for (var date = openingDate; date <= today; date = date.AddDays(1))
            {
                var offset = date.DayNumber - openingDate.DayNumber;
                if (date.Day is 1 or 15)
                    await Add(checking.Id, salary.Id, date, 3200m, "Paycheck", "Demo Employer");
                if (date.Day == 1)
                    await Add(checking.Id, housing.Id, date, 1850m, "Monthly rent", "Demo Property Management");
                if (date.Day == 8)
                    await Add(checking.Id, utilities.Id, date, 165.40m, "Electricity and internet", "Demo Utilities");
                if (date.Day == 20)
                    await Add(retirement.Id, contributions.Id, date, 650m, "Employer retirement contribution", "Demo Employer");
                if (date.Day == 25)
                    await Add(retirement.Id, fees.Id, date, 24.50m, "Plan administration fee", "Demo Retirement");
                if (date.Day == DateTime.DaysInMonth(date.Year, date.Month))
                    await Add(savings.Id, interest.Id, date, 28.75m, "Monthly savings interest", "Demo Community Bank");
                if (date.DayOfWeek == DayOfWeek.Thursday)
                    await Add(card.Id, groceries.Id, date, 92.35m + offset % 5 * 7m, "Weekly groceries", "Demo Market");
                if (date.DayOfWeek == DayOfWeek.Saturday)
                    await Add(checking.Id, dining.Id, date, 38.50m + offset % 4 * 6m, "Weekend lunch", "Demo Cafe");
                if (date.DayOfWeek == DayOfWeek.Tuesday)
                    await Add(checking.Id, transport.Id, date, 42.80m, "Fuel", "Demo Fuel");

                // Daily activity keeps yesterday/today and weekly chart ranges useful.
                await Add(checking.Id, dining.Id, date, 5.25m + offset % 3 * 0.75m, "Morning coffee", "Demo Coffee");
            }

            Console.WriteLine($"Demo data ready: {Email}; household Ross; 4 accounts; " +
                $"{await context.Transactions.CountAsync()} transactions; history {openingDate:yyyy-MM-dd} through {today:yyyy-MM-dd}.");
        }
        finally
        {
            httpContextAccessor.HttpContext = previousContext;
        }
    }
}
