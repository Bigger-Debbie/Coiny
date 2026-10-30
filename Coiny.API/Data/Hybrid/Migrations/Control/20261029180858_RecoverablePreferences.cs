using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Hybrid.Migrations.Control
{
    /// <inheritdoc />
    public partial class RecoverablePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingPreferenceChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    IncludeMortgage = table.Column<bool>(type: "boolean", nullable: false),
                    ScanEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExpectedVersion = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingPreferenceChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingPreferenceChanges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingPreferenceChanges_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingPreferenceChanges_HouseholdId",
                table: "PendingPreferenceChanges",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingPreferenceChanges_UserId",
                table: "PendingPreferenceChanges",
                column: "UserId",
                unique: true,
                filter: "\"CompletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingPreferenceChanges");
        }
    }
}
