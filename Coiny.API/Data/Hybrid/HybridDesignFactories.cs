using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coiny.API.Data.Hybrid;

// Scaffolding requires no live DB, application startup, user secrets or real data.
public sealed class ControlDesignFactory : IDesignTimeDbContextFactory<ControlContext>
{
    public ControlContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ControlContext>()
        .UseNpgsql("Host=localhost;Database=coiny_design_only;Username=design_only;Password=design_only").Options);
}
public sealed class HouseholdDesignFactory : IDesignTimeDbContextFactory<HouseholdContext>
{
    public HouseholdContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<HouseholdContext>()
        .UseSqlite("Data Source=:memory:").Options);
}
