using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountReconciliations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountReconciliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StatementBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LedgerFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConfirmedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ConfirmedByName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    InvalidatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InvalidationReason = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountReconciliations_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountReconciliations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountReconciliations_AccountId_StatementDate_LedgerFingerprint",
                table: "AccountReconciliations",
                columns: new[] { "AccountId", "StatementDate", "LedgerFingerprint" },
                unique: true,
                filter: "\"InvalidatedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountReconciliations_HouseholdId",
                table: "AccountReconciliations",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountReconciliations");
        }
    }
}
