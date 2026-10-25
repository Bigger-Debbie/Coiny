using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class HouseholdRecurringSettingConfiguration : IEntityTypeConfiguration<HouseholdRecurringSetting>
{
    public void Configure(EntityTypeBuilder<HouseholdRecurringSetting> b) {
        b.HasIndex(r=>r.HouseholdId).IsUnique(); b.Property(r=>r.EditVersion).IsConcurrencyToken();
        b.HasOne(r=>r.Household).WithMany().HasForeignKey(r=>r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class HouseholdAlertConfiguration : IEntityTypeConfiguration<HouseholdAlert>
{
    public void Configure(EntityTypeBuilder<HouseholdAlert> b) {
        b.HasIndex(r=>new {r.HouseholdId,r.PatternKey}).IsUnique(); b.Property(r=>r.PatternKey).HasMaxLength(64);
        b.Property(r=>r.Merchant).HasMaxLength(500); b.Property(r=>r.Frequency).HasMaxLength(30);
        b.HasOne(r=>r.Household).WithMany().HasForeignKey(r=>r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class HouseholdAlertReadConfiguration : IEntityTypeConfiguration<HouseholdAlertRead>
{
    public void Configure(EntityTypeBuilder<HouseholdAlertRead> b) {
        b.HasKey(r=>new {r.AlertId,r.UserId}); b.Property(r=>r.Version).IsConcurrencyToken();
        b.HasOne(r=>r.Alert).WithMany().HasForeignKey(r=>r.AlertId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r=>r.User).WithMany().HasForeignKey(r=>r.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
