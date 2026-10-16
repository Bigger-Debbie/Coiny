using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetDraftEditVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EditVersion",
                table: "BudgetRevisions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetRevisions_EditVersion",
                table: "BudgetRevisions",
                sql: "\"EditVersion\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetRevisions_EditVersion",
                table: "BudgetRevisions");

            migrationBuilder.DropColumn(
                name: "EditVersion",
                table: "BudgetRevisions");
        }
    }
}
