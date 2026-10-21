using Coiny.API.Enums;
using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class BudgetRevisionConfiguration : IEntityTypeConfiguration<BudgetRevision>
{
    public void Configure(EntityTypeBuilder<BudgetRevision> builder)
    {
        builder.Property(r => r.EditVersion).IsConcurrencyToken();
        builder.HasOne(r => r.HouseholdBudget).WithMany(b => b.Revisions)
            .HasForeignKey(r => r.HouseholdBudgetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.HouseholdBudgetId, r.RevisionNumber }).IsUnique();
        builder.HasIndex(r => r.HouseholdBudgetId).IsUnique()
            .HasDatabaseName("IX_BudgetRevisions_OneDraftPerBudget")
            .HasFilter($"\"State\" = {(int)BudgetRevisionState.Draft}");
        builder.HasIndex(r => new { r.HouseholdBudgetId, r.EffectiveFromMonth });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_BudgetRevisions_RevisionNumber", "\"RevisionNumber\" > 0");
            table.HasCheckConstraint("CK_BudgetRevisions_EditVersion", "\"EditVersion\" >= 0");
            table.HasCheckConstraint("CK_BudgetRevisions_EffectiveMonth",
                "substr(\"EffectiveFromMonth\", 9, 2) = '01'");
            table.HasCheckConstraint("CK_BudgetRevisions_State",
                "(\"State\" = 0 AND \"PublishedAt\" IS NULL) OR (\"State\" = 1 AND \"PublishedAt\" IS NOT NULL)");
            table.HasCheckConstraint("CK_BudgetRevisions_SetupStep", "\"SetupStep\" BETWEEN 0 AND 5");
        });
    }
}
