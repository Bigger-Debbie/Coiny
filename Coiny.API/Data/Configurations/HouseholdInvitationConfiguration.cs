using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
    {
        builder.HasIndex(i => i.HouseholdId).IsUnique();
        builder.HasIndex(i => i.CodeHash).IsUnique();
        builder.Property(i => i.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasOne(i => i.Household).WithMany().HasForeignKey(i => i.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(i => i.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_HouseholdInvitations_CodeHash", "length(\"CodeHash\") = 64"));
    }
}
