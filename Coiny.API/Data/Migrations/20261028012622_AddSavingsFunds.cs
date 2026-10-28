using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsFunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SavingsPlanJson",
                table: "BudgetAllocations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SavingsFund",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsFund", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsFund_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavingsActivity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SavingsFundId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComponentKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsActivity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsActivity_SavingsFund_SavingsFundId",
                        column: x => x.SavingsFundId,
                        principalTable: "SavingsFund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavingsActivity_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsActivity_SavingsFundId_OperationId_ComponentKey",
                table: "SavingsActivity",
                columns: new[] { "SavingsFundId", "OperationId", "ComponentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsActivity_TransactionId",
                table: "SavingsActivity",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsFund_AccountId",
                table: "SavingsFund",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsFund_HouseholdId_SourceKey",
                table: "SavingsFund",
                columns: new[] { "HouseholdId", "SourceKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavingsActivity");

            migrationBuilder.DropTable(
                name: "SavingsFund");

            migrationBuilder.DropColumn(
                name: "SavingsPlanJson",
                table: "BudgetAllocations");
        }
    }
}
