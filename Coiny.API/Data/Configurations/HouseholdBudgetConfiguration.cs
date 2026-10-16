using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class HouseholdBudgetConfiguration : IEntityTypeConfiguration<HouseholdBudget>
{
    public void Configure(EntityTypeBuilder<HouseholdBudget> builder)
    {
        builder.HasIndex(b => b.HouseholdId).IsUnique();
        builder.HasOne(b => b.Household).WithOne(h => h.Budget)
            .HasForeignKey<HouseholdBudget>(b => b.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
