using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coiny.API.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasOne(t => t.PairedTransaction).WithMany()
            .HasForeignKey(t => t.PairedTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.PairedTransactionId).IsUnique();
        builder.Property(t => t.PairedTransactionId).IsConcurrencyToken();
        builder.ToTable(t => t.HasCheckConstraint("CK_Transaction_TransferMetadata",
            "(TransferDirection IS NULL AND TransferCounterparty IS NULL AND PairedTransactionId IS NULL) OR " +
            "(TransferDirection IS NOT NULL AND TransferCounterparty IS NOT NULL AND TransferDirection IN (0,1) AND TransferCounterparty IN (0,1) AND PairedTransactionId IS NULL) OR " +
            "(TransferDirection IS NOT NULL AND TransferCounterparty IS NOT NULL AND TransferDirection IN (0,1) AND TransferCounterparty = 2 AND PairedTransactionId IS NOT NULL AND PairedTransactionId <> Id)"));
        builder.Property(t => t.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(t => t.Merchant)
            .HasMaxLength(150);

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        builder.HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
