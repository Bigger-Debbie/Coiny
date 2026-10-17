using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionImportBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportBatchId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportRowId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ImportedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionImportBatches_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionImportBatches_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportBatchId_ImportRowId",
                table: "Transactions",
                columns: new[] { "ImportBatchId", "ImportRowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionImportBatches_AccountId",
                table: "TransactionImportBatches",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionImportBatches_HouseholdId_SubmissionId",
                table: "TransactionImportBatches",
                columns: new[] { "HouseholdId", "SubmissionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_TransactionImportBatches_ImportBatchId",
                table: "Transactions",
                column: "ImportBatchId",
                principalTable: "TransactionImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_TransactionImportBatches_ImportBatchId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "TransactionImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ImportBatchId_ImportRowId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ImportRowId",
                table: "Transactions");
        }
    }
}
