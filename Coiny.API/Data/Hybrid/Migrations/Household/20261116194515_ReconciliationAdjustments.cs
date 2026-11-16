using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Hybrid.Migrations.Household
{
    /// <inheritdoc />
    public partial class ReconciliationAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmount",
                table: "AccountReconciliations",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AdjustmentOperationId",
                table: "AccountReconciliations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReason",
                table: "AccountReconciliations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReviewedFingerprint",
                table: "AccountReconciliations",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdjustmentTransactionId",
                table: "AccountReconciliations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountReconciliations_AdjustmentTransactionId",
                table: "AccountReconciliations",
                column: "AdjustmentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountReconciliations_HouseholdId_AdjustmentOperationId",
                table: "AccountReconciliations",
                columns: new[] { "HouseholdId", "AdjustmentOperationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountReconciliations_Transactions_AdjustmentTransactionId",
                table: "AccountReconciliations",
                column: "AdjustmentTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountReconciliations_Transactions_AdjustmentTransactionId",
                table: "AccountReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_AccountReconciliations_AdjustmentTransactionId",
                table: "AccountReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_AccountReconciliations_HouseholdId_AdjustmentOperationId",
                table: "AccountReconciliations");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmount",
                table: "AccountReconciliations");

            migrationBuilder.DropColumn(
                name: "AdjustmentOperationId",
                table: "AccountReconciliations");

            migrationBuilder.DropColumn(
                name: "AdjustmentReason",
                table: "AccountReconciliations");

            migrationBuilder.DropColumn(
                name: "AdjustmentReviewedFingerprint",
                table: "AccountReconciliations");

            migrationBuilder.DropColumn(
                name: "AdjustmentTransactionId",
                table: "AccountReconciliations");
        }
    }
}
