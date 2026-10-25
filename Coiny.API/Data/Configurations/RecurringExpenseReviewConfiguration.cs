using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class RecurringExpenseReviewConfiguration : IEntityTypeConfiguration<RecurringExpenseReview>
{
    public void Configure(EntityTypeBuilder<RecurringExpenseReview> builder)
    {
        builder.HasIndex(r => new { r.HouseholdId, r.PatternKey }).IsUnique();
        builder.Property(r => r.PatternKey).HasMaxLength(64);
        builder.HasOne(r => r.Household).WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}
