using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class SavingsFundConfiguration : IEntityTypeConfiguration<SavingsFund>, IEntityTypeConfiguration<SavingsActivity>
{
    public void Configure(EntityTypeBuilder<SavingsFund> b)
    {
        b.HasIndex(f => new { f.HouseholdId, f.SourceKey }).IsUnique();
        b.Property(f => f.Name).HasMaxLength(100).IsRequired();
        b.Property(f => f.EditVersion).IsConcurrencyToken();
        b.HasOne(f => f.Account).WithMany().HasForeignKey(f => f.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<SavingsActivity> b)
    {
        b.HasOne(a => a.SavingsFund).WithMany(f => f.Activities).HasForeignKey(a => a.SavingsFundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Transaction).WithMany().HasForeignKey(a => a.TransactionId).OnDelete(DeleteBehavior.SetNull);
        b.Property(a => a.Amount).HasPrecision(18,2);
        b.Property(a => a.Note).HasMaxLength(500);
        b.HasIndex(a => new { a.SavingsFundId, a.OperationId, a.ComponentKey }).IsUnique();
    }
}
