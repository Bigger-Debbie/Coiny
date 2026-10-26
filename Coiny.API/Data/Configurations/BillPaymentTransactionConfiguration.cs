using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class BillPaymentTransactionConfiguration : IEntityTypeConfiguration<BillPaymentTransaction>
{
    public void Configure(EntityTypeBuilder<BillPaymentTransaction> builder)
    {
        builder.HasIndex(x => x.TransactionId).IsUnique();
        builder.HasOne(x => x.BillPayment).WithMany(x => x.Links).HasForeignKey(x => x.BillPaymentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
    }
}
