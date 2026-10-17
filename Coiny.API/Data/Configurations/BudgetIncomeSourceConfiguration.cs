using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class BudgetIncomeSourceConfiguration : IEntityTypeConfiguration<BudgetIncomeSource>
{
    public void Configure(EntityTypeBuilder<BudgetIncomeSource> builder)
    {
        builder.HasIndex(i => new { i.BudgetRevisionId, i.SourceKey }).IsUnique();
        builder.Property(i => i.Name).HasMaxLength(100).IsRequired();
        builder.Property(i => i.Amount).HasPrecision(18, 2);
        builder.Property(i => i.HealthInsurancePerPaycheck).HasPrecision(18, 2);
        builder.Property(i => i.RetirementContributionPerPaycheck).HasPrecision(18, 2);
        builder.HasOne(i => i.BudgetRevision).WithMany(r => r.IncomeSources)
            .HasForeignKey(i => i.BudgetRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(i => i.Schedule, schedule => BudgetScheduleMapping.Configure(schedule));
        builder.Navigation(i => i.Schedule).IsRequired();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_BudgetIncomeSources_Name", "length(trim(\"Name\")) BETWEEN 1 AND 100");
            table.HasCheckConstraint("CK_BudgetIncomeSources_Amount", "CAST(\"Amount\" AS REAL) >= 0");
            table.HasCheckConstraint("CK_BudgetIncomeSources_HealthInsurance", "\"HealthInsurancePerPaycheck\" IS NULL OR CAST(\"HealthInsurancePerPaycheck\" AS REAL) >= 0");
            table.HasCheckConstraint("CK_BudgetIncomeSources_RetirementContribution", "\"RetirementContributionPerPaycheck\" IS NULL OR CAST(\"RetirementContributionPerPaycheck\" AS REAL) >= 0");
            table.HasCheckConstraint("CK_BudgetIncomeSources_Schedule", BudgetScheduleMapping.Constraint(false));
        });
    }
}
