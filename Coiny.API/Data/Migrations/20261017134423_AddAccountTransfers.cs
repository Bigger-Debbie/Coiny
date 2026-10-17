using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PairedTransactionId",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferCounterparty",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TransferDirection",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PairedTransactionId",
                table: "Transactions",
                column: "PairedTransactionId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transaction_TransferMetadata",
                table: "Transactions",
                sql: "(TransferDirection IS NULL AND TransferCounterparty IS NULL AND PairedTransactionId IS NULL) OR (TransferDirection IS NOT NULL AND TransferCounterparty IS NOT NULL AND TransferDirection IN (0,1) AND TransferCounterparty IN (0,1) AND PairedTransactionId IS NULL) OR (TransferDirection IS NOT NULL AND TransferCounterparty IS NOT NULL AND TransferDirection IN (0,1) AND TransferCounterparty = 2 AND PairedTransactionId IS NOT NULL AND PairedTransactionId <> Id)");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Transactions_PairedTransactionId",
                table: "Transactions",
                column: "PairedTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Transactions_PairedTransactionId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_PairedTransactionId",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transaction_TransferMetadata",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "PairedTransactionId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransferCounterparty",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransferDirection",
                table: "Transactions");
        }
    }
}
