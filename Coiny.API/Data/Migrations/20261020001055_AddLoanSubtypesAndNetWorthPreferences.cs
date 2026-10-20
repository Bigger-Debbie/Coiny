using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanSubtypesAndNetWorthPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeMortgageInNetWorth",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LoanSubtype",
                table: "Accounts",
                type: "INTEGER",
                nullable: true);

            // Legacy loans remain Other until explicitly classified by a household member.
            migrationBuilder.Sql("UPDATE Accounts SET LoanSubtype = 0 WHERE AccountType = 5 AND LoanSubtype IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_LoanSubtype",
                table: "Accounts",
                sql: "LoanSubtype IS NULL OR (AccountType = 5 AND LoanSubtype BETWEEN 0 AND 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_LoanSubtype",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "IncludeMortgageInNetWorth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LoanSubtype",
                table: "Accounts");
        }
    }
}
