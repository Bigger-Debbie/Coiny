using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class BillPaymentConfiguration : IEntityTypeConfiguration<BillPayment>
{
    public void Configure(EntityTypeBuilder<BillPayment> builder)
    {
        builder.HasIndex(p => new { p.HouseholdId, p.AllocationKey, p.DueDate }).IsUnique();
        // Legacy primary ledger reference; additional entries are stored in BillPaymentTransaction.
        builder.HasIndex(p => p.TransactionId).IsUnique();
        builder.Property(p => p.Note).HasMaxLength(500);
        builder.HasOne(p => p.Household).WithMany().HasForeignKey(p => p.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Transaction).WithMany().HasForeignKey(p => p.TransactionId).OnDelete(DeleteBehavior.SetNull);
    }
}
