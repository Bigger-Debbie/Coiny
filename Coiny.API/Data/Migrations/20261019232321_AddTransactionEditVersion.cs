using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionEditVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EditVersion",
                table: "Transactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EditVersion",
                table: "Transactions");
        }
    }
}
