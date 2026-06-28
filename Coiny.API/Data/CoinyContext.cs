using Coiny.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coiny.API.Data;

public class CoinyContext : IdentityDbContext<ApplicationUser>
{
    public CoinyContext(DbContextOptions<CoinyContext> options) : base(options) {}

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(CoinyContext).Assembly);
    }
}