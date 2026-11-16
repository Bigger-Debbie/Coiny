using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;

public class AccountReconciliationConfiguration : IEntityTypeConfiguration<AccountReconciliation>
{
    public void Configure(EntityTypeBuilder<AccountReconciliation> builder)
    {
        builder.Property(r => r.StatementBalance).HasColumnType("decimal(18,2)");
        builder.Property(r => r.LedgerFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ConfirmedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(r => r.ConfirmedByName).HasMaxLength(256).IsRequired();
        builder.Property(r => r.AdjustmentAmount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.AdjustmentReason).HasMaxLength(500);
        builder.Property(r => r.AdjustmentReviewedFingerprint).HasMaxLength(64);
        builder.HasIndex(r => new {r.HouseholdId,r.AdjustmentOperationId}).IsUnique();
        builder.HasOne<Transaction>().WithMany().HasForeignKey(r => r.AdjustmentTransactionId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(r => r.InvalidationReason).HasMaxLength(250);
        builder.HasOne(r => r.Account).WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Household).WithMany().HasForeignKey(r => r.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.AccountId, r.StatementDate, r.LedgerFingerprint }).IsUnique().HasFilter("\"InvalidatedAt\" IS NULL");
    }
}
