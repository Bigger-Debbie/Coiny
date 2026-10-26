using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedCsvMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedCsvMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AccountType = table.Column<int>(type: "INTEGER", nullable: false),
                    InstitutionId = table.Column<int>(type: "INTEGER", nullable: true),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedCsvMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedCsvMappings_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedCsvMappings_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedCsvMappings_HouseholdId_NormalizedName",
                table: "SavedCsvMappings",
                columns: new[] { "HouseholdId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedCsvMappings_InstitutionId",
                table: "SavedCsvMappings",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedCsvMappings");
        }
    }
}
