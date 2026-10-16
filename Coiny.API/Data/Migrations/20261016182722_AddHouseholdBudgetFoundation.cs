using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdBudgetFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdBudgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdBudgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdBudgets_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdBudgetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveFromMonth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    SetupStep = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetRevisions", x => x.Id);
                    table.CheckConstraint("CK_BudgetRevisions_EffectiveMonth", "substr(\"EffectiveFromMonth\", 9, 2) = '01'");
                    table.CheckConstraint("CK_BudgetRevisions_RevisionNumber", "\"RevisionNumber\" > 0");
                    table.CheckConstraint("CK_BudgetRevisions_SetupStep", "\"SetupStep\" BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_BudgetRevisions_State", "(\"State\" = 0 AND \"PublishedAt\" IS NULL) OR (\"State\" = 1 AND \"PublishedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BudgetRevisions_HouseholdBudgets_HouseholdBudgetId",
                        column: x => x.HouseholdBudgetId,
                        principalTable: "HouseholdBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BudgetRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AllocationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ScheduleFrequency = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduleStartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ScheduleEndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ScheduleDayOfMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduleSecondDayOfMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetAllocations", x => x.Id);
                    table.CheckConstraint("CK_BudgetAllocations_Amount", "CAST(\"Amount\" AS REAL) >= 0");
                    table.CheckConstraint("CK_BudgetAllocations_Name", "length(trim(\"Name\")) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_BudgetAllocations_Schedule", "(\"ScheduleFrequency\" IS NULL AND \"ScheduleStartDate\" IS NULL AND \"ScheduleEndDate\" IS NULL\nAND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL) OR COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");
                    table.CheckConstraint("CK_BudgetAllocations_Type", "\"AllocationType\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_BudgetAllocations_BudgetRevisions_BudgetRevisionId",
                        column: x => x.BudgetRevisionId,
                        principalTable: "BudgetRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetIncomeSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BudgetRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    ScheduleFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduleStartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ScheduleEndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ScheduleDayOfMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    ScheduleSecondDayOfMonth = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetIncomeSources", x => x.Id);
                    table.CheckConstraint("CK_BudgetIncomeSources_Amount", "CAST(\"Amount\" AS REAL) >= 0");
                    table.CheckConstraint("CK_BudgetIncomeSources_Name", "length(trim(\"Name\")) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_BudgetIncomeSources_Schedule", "COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");
                    table.ForeignKey(
                        name: "FK_BudgetIncomeSources_BudgetRevisions_BudgetRevisionId",
                        column: x => x.BudgetRevisionId,
                        principalTable: "BudgetRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAllocations_BudgetRevisionId",
                table: "BudgetAllocations",
                column: "BudgetRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetIncomeSources_BudgetRevisionId",
                table: "BudgetIncomeSources",
                column: "BudgetRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRevisions_HouseholdBudgetId_EffectiveFromMonth",
                table: "BudgetRevisions",
                columns: new[] { "HouseholdBudgetId", "EffectiveFromMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRevisions_HouseholdBudgetId_RevisionNumber",
                table: "BudgetRevisions",
                columns: new[] { "HouseholdBudgetId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetRevisions_OneDraftPerBudget",
                table: "BudgetRevisions",
                column: "HouseholdBudgetId",
                unique: true,
                filter: "\"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdBudgets_HouseholdId",
                table: "HouseholdBudgets",
                column: "HouseholdId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetAllocations");

            migrationBuilder.DropTable(
                name: "BudgetIncomeSources");

            migrationBuilder.DropTable(
                name: "BudgetRevisions");

            migrationBuilder.DropTable(
                name: "HouseholdBudgets");
        }
    }
}
