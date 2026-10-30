using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Hybrid.Migrations.Control
{
    /// <inheritdoc />
    public partial class InstallationMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstallationMaintenance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationMaintenance", x => x.Id);
                    table.CheckConstraint("CK_InstallationMaintenance_Singleton", "\"Id\"=1");
                });

            migrationBuilder.InsertData(
                table: "InstallationMaintenance",
                columns: new[] { "Id", "OperationId", "RequestedAt" },
                values: new object[] { 1, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstallationMaintenance");
        }
    }
}
