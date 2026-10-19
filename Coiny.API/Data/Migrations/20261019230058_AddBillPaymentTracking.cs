using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetAllocations_BudgetRevisionId",
                table: "BudgetAllocations");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceKey",
                table: "BudgetAllocations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing allocations get distinct nonempty identities before the unique index is created.
            migrationBuilder.Sql("UPDATE BudgetAllocations SET SourceKey = upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)))");

            migrationBuilder.CreateTable(
                name: "BillPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsPaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillPayments_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BillPayments_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAllocations_BudgetRevisionId_SourceKey",
                table: "BudgetAllocations",
                columns: new[] { "BudgetRevisionId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_HouseholdId_AllocationKey_DueDate",
                table: "BillPayments",
                columns: new[] { "HouseholdId", "AllocationKey", "DueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_TransactionId",
                table: "BillPayments",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillPayments");

            migrationBuilder.DropIndex(
                name: "IX_BudgetAllocations_BudgetRevisionId_SourceKey",
                table: "BudgetAllocations");

            migrationBuilder.DropColumn(
                name: "SourceKey",
                table: "BudgetAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAllocations_BudgetRevisionId",
                table: "BudgetAllocations",
                column: "BudgetRevisionId");
        }
    }
}
