using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringAutomationAndAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    PatternKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Merchant = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    TypicalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    NextExpected = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdAlerts_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdRecurringSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastScanDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdRecurringSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdRecurringSettings_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdAlertReads",
                columns: table => new
                {
                    AlertId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdAlertReads", x => new { x.AlertId, x.UserId });
                    table.ForeignKey(
                        name: "FK_HouseholdAlertReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdAlertReads_HouseholdAlerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "HouseholdAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAlertReads_UserId",
                table: "HouseholdAlertReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAlerts_HouseholdId_PatternKey",
                table: "HouseholdAlerts",
                columns: new[] { "HouseholdId", "PatternKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRecurringSettings_HouseholdId",
                table: "HouseholdRecurringSettings",
                column: "HouseholdId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdAlertReads");

            migrationBuilder.DropTable(
                name: "HouseholdRecurringSettings");

            migrationBuilder.DropTable(
                name: "HouseholdAlerts");
        }
    }
}
