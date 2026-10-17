using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetIncomeSourceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetIncomeSources_BudgetRevisionId",
                table: "BudgetIncomeSources");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceKey",
                table: "BudgetIncomeSources",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing revisions have no reliable cross-version source identity. Give
            // each existing row a key; future draft copies preserve it explicitly.
            migrationBuilder.Sql("UPDATE BudgetIncomeSources SET SourceKey = lower(hex(randomblob(16)))");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetIncomeSources_BudgetRevisionId_SourceKey",
                table: "BudgetIncomeSources",
                columns: new[] { "BudgetRevisionId", "SourceKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetIncomeSources_BudgetRevisionId_SourceKey",
                table: "BudgetIncomeSources");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "BudgetIncomeSources");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetIncomeSources_BudgetRevisionId",
                table: "BudgetIncomeSources",
                column: "BudgetRevisionId");
        }
    }
}
