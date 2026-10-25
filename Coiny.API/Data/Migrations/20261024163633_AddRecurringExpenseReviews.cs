using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringExpenseReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringExpenseReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    PatternKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Dismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringExpenseReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringExpenseReviews_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenseReviews_HouseholdId_PatternKey",
                table: "RecurringExpenseReviews",
                columns: new[] { "HouseholdId", "PatternKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringExpenseReviews");
        }
    }
}
