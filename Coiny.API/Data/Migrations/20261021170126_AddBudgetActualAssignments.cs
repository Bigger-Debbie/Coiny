using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetActualAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetCategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveFromMonth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCategoryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetCategoryRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetCategoryRules_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionBudgetAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionBudgetAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionBudgetAssignments_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionBudgetAssignments_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategoryRules_CategoryId",
                table: "BudgetCategoryRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategoryRules_HouseholdId_CategoryId_EffectiveFromMonth",
                table: "BudgetCategoryRules",
                columns: new[] { "HouseholdId", "CategoryId", "EffectiveFromMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionBudgetAssignments_HouseholdId",
                table: "TransactionBudgetAssignments",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionBudgetAssignments_TransactionId",
                table: "TransactionBudgetAssignments",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetCategoryRules");

            migrationBuilder.DropTable(
                name: "TransactionBudgetAssignments");
        }
    }
}
