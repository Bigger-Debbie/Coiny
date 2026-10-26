using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class SavedCsvMappingConfiguration:IEntityTypeConfiguration<SavedCsvMapping> {
    public void Configure(EntityTypeBuilder<SavedCsvMapping> b) {
        b.HasIndex(p=>new {p.HouseholdId,p.NormalizedName}).IsUnique();b.Property(p=>p.Name).HasMaxLength(100);b.Property(p=>p.NormalizedName).HasMaxLength(200);b.Property(p=>p.EditVersion).IsConcurrencyToken();
        b.HasOne(p=>p.Household).WithMany().HasForeignKey(p=>p.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p=>p.Institution).WithMany().HasForeignKey(p=>p.InstitutionId).OnDelete(DeleteBehavior.SetNull);
    }
}
