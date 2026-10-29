using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class AuthRecoveryCodeConfiguration : IEntityTypeConfiguration<AuthRecoveryCode>
{
    public void Configure(EntityTypeBuilder<AuthRecoveryCode> builder)
    {
        builder.HasKey(code => code.Id);
        builder.HasIndex(code => new { code.UserId, code.CodeHash }).IsUnique();
        builder.HasOne(code => code.User).WithMany().HasForeignKey(code => code.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
