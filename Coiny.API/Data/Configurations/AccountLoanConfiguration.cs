using Coiny.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Coiny.API.Data.Configurations;
public class AccountLoanConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder) => builder.ToTable(t => t.HasCheckConstraint("CK_Accounts_LoanSubtype", "LoanSubtype IS NULL OR (AccountType = 5 AND LoanSubtype BETWEEN 0 AND 4)"));
}
public class UserPreferenceConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder) => builder.Property(u => u.IncludeMortgageInNetWorth).HasDefaultValue(true);
}
