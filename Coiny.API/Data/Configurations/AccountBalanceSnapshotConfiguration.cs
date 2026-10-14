using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class AccountBalanceSnapshotConfiguration : IEntityTypeConfiguration<AccountBalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<AccountBalanceSnapshot> builder)
    {
        builder.Property(snapshot => snapshot.SnapshotDate)
            .IsRequired();

        builder.Property(snapshot => snapshot.Balance)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.HasIndex(snapshot => new { snapshot.AccountId, snapshot.SnapshotDate })
            .IsUnique();

        builder.HasOne(snapshot => snapshot.Account)
            .WithMany(account => account.BalanceSnapshots)
            .HasForeignKey(snapshot => snapshot.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
