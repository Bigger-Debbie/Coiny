using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitSavingsWizardSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions",
                sql: "\"SetupStep\" BETWEEN 0 AND 7");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions",
                sql: "\"SetupStep\" BETWEEN 0 AND 5");
        }
    }
}
