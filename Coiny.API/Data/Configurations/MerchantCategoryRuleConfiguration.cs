using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class MerchantCategoryRuleConfiguration : IEntityTypeConfiguration<MerchantCategoryRule>
{
    public void Configure(EntityTypeBuilder<MerchantCategoryRule> b) {
        b.HasIndex(r=>new {r.HouseholdId,r.MatchType,r.NormalizedPattern}).IsUnique();
        b.Property(r=>r.MatchType).HasMaxLength(30); b.Property(r=>r.Pattern).HasMaxLength(200);b.Property(r=>r.NormalizedPattern).HasMaxLength(200);
        b.Property(r=>r.EditVersion).IsConcurrencyToken();
        b.HasOne(r=>r.Household).WithMany().HasForeignKey(r=>r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r=>r.Category).WithMany().HasForeignKey(r=>r.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
