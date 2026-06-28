using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.OpeningBalance)
            .HasColumnType("decimal(18,2)")
            .IsRequired(); 

        builder.HasOne(a => a.Institution)
            .WithMany(i => i.Accounts)
            .HasForeignKey(a => a.InstitutionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}