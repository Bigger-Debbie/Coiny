using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class BudgetAllocationConfiguration : IEntityTypeConfiguration<BudgetAllocation>
{
    public void Configure(EntityTypeBuilder<BudgetAllocation> builder)
    {
        builder.HasIndex(a => new { a.BudgetRevisionId, a.SourceKey }).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Amount).HasPrecision(18, 2);
        builder.HasOne(a => a.BudgetRevision).WithMany(r => r.Allocations)
            .HasForeignKey(a => a.BudgetRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(a => a.Schedule, schedule => BudgetScheduleMapping.Configure(schedule));
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_BudgetAllocations_Name", "length(trim(\"Name\")) BETWEEN 1 AND 100");
            table.HasCheckConstraint("CK_BudgetAllocations_Amount", "CAST(\"Amount\" AS REAL) >= 0");
            table.HasCheckConstraint("CK_BudgetAllocations_Type", "\"AllocationType\" BETWEEN 0 AND 5");
            table.HasCheckConstraint("CK_BudgetAllocations_Schedule", BudgetScheduleMapping.Constraint(true));
        });
    }
}
