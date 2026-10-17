using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class TransactionImportBatchConfiguration : IEntityTypeConfiguration<TransactionImportBatch>
{
    public void Configure(EntityTypeBuilder<TransactionImportBatch> builder)
    {
        builder.HasIndex(b => new { b.HouseholdId, b.SubmissionId }).IsUnique();
        builder.Property(b => b.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(b => b.Filename).HasMaxLength(255).IsRequired();
        builder.Property(b => b.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(b => b.BalanceAfter).HasColumnType("decimal(18,2)");
        builder.HasOne(b => b.Household).WithMany().HasForeignKey(b => b.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Account).WithMany().HasForeignKey(b => b.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
