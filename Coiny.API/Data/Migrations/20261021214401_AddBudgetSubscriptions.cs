using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetAllocations_Type",
                table: "BudgetAllocations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions",
                sql: "\"SetupStep\" BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetAllocations_Type",
                table: "BudgetAllocations",
                sql: "\"AllocationType\" BETWEEN 0 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetAllocations_Type",
                table: "BudgetAllocations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetRevisions_SetupStep",
                table: "BudgetRevisions",
                sql: "\"SetupStep\" BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetAllocations_Type",
                table: "BudgetAllocations",
                sql: "\"AllocationType\" BETWEEN 0 AND 4");
        }
    }
}
