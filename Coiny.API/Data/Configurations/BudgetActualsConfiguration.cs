using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class BudgetCategoryRuleConfiguration : IEntityTypeConfiguration<BudgetCategoryRule>
{
    public void Configure(EntityTypeBuilder<BudgetCategoryRule> builder)
    {
        builder.HasIndex(r => new { r.HouseholdId, r.CategoryId, r.EffectiveFromMonth }).IsUnique();
        builder.Property(r => r.EditVersion).IsConcurrencyToken();
        builder.HasOne(r => r.Household).WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransactionBudgetAssignmentConfiguration : IEntityTypeConfiguration<TransactionBudgetAssignment>
{
    public void Configure(EntityTypeBuilder<TransactionBudgetAssignment> builder)
    {
        builder.HasIndex(r => r.TransactionId).IsUnique();
        builder.HasOne(r => r.Household).WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Transaction).WithMany().HasForeignKey(r => r.TransactionId).OnDelete(DeleteBehavior.Cascade);
    }
}
