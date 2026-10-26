using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class TransactionImportUndoConfiguration : IEntityTypeConfiguration<TransactionImportUndo>
{
    public void Configure(EntityTypeBuilder<TransactionImportUndo> b)
    {
        b.HasIndex(x=>new{x.HouseholdId,x.OperationId}).IsUnique();
        b.Property(x=>x.RequestHash).HasMaxLength(64); b.Property(x=>x.ActorUserId).HasMaxLength(450);
        b.HasOne(x=>x.Household).WithMany().HasForeignKey(x=>x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
    }
}
