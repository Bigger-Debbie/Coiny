using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class TransactionImportSessionConfiguration : IEntityTypeConfiguration<TransactionImportSession>
{
    public void Configure(EntityTypeBuilder<TransactionImportSession> builder)
    {
        builder.HasIndex(s => new { s.HouseholdId, s.SubmissionId }).IsUnique();
        builder.Property(s => s.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.ResultJson).IsRequired();
        builder.HasOne(s => s.Household).WithMany().HasForeignKey(s => s.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}
