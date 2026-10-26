using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleBillTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BillPaymentTransaction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BillPaymentId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillPaymentTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillPaymentTransaction_BillPayments_BillPaymentId",
                        column: x => x.BillPaymentId,
                        principalTable: "BillPayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillPaymentTransaction_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillPaymentTransaction_BillPaymentId",
                table: "BillPaymentTransaction",
                column: "BillPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPaymentTransaction_TransactionId",
                table: "BillPaymentTransaction",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillPaymentTransaction");
        }
    }
}
