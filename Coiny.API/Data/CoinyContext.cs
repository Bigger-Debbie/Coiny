using Coiny.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data;

public class CoinyContext : IdentityDbContext<ApplicationUser>
{
    public CoinyContext(DbContextOptions<CoinyContext> options) : base(options) {}

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();
    public DbSet<HouseholdBudget> HouseholdBudgets => Set<HouseholdBudget>();
    public DbSet<BudgetRevision> BudgetRevisions => Set<BudgetRevision>();
    public DbSet<BudgetIncomeSource> BudgetIncomeSources => Set<BudgetIncomeSource>();
    public DbSet<BudgetAllocation> BudgetAllocations => Set<BudgetAllocation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(CoinyContext).Assembly);
    }
}
