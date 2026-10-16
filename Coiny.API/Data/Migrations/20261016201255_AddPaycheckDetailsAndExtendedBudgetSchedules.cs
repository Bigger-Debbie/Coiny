using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaycheckDetailsAndExtendedBudgetSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetIncomeSources_Schedule",
                table: "BudgetIncomeSources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetAllocations_Schedule",
                table: "BudgetAllocations");

            migrationBuilder.AddColumn<decimal>(
                name: "HealthInsurancePerPaycheck",
                table: "BudgetIncomeSources",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RetirementContributionPerPaycheck",
                table: "BudgetIncomeSources",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetIncomeSources_HealthInsurance",
                table: "BudgetIncomeSources",
                sql: "\"HealthInsurancePerPaycheck\" IS NULL OR CAST(\"HealthInsurancePerPaycheck\" AS REAL) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetIncomeSources_RetirementContribution",
                table: "BudgetIncomeSources",
                sql: "\"RetirementContributionPerPaycheck\" IS NULL OR CAST(\"RetirementContributionPerPaycheck\" AS REAL) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetIncomeSources_Schedule",
                table: "BudgetIncomeSources",
                sql: "COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1, 4, 5) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetAllocations_Schedule",
                table: "BudgetAllocations",
                sql: "(\"ScheduleFrequency\" IS NULL AND \"ScheduleStartDate\" IS NULL AND \"ScheduleEndDate\" IS NULL\nAND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL) OR COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1, 4, 5) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetIncomeSources_HealthInsurance",
                table: "BudgetIncomeSources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetIncomeSources_RetirementContribution",
                table: "BudgetIncomeSources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetIncomeSources_Schedule",
                table: "BudgetIncomeSources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetAllocations_Schedule",
                table: "BudgetAllocations");

            migrationBuilder.DropColumn(
                name: "HealthInsurancePerPaycheck",
                table: "BudgetIncomeSources");

            migrationBuilder.DropColumn(
                name: "RetirementContributionPerPaycheck",
                table: "BudgetIncomeSources");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetIncomeSources_Schedule",
                table: "BudgetIncomeSources",
                sql: "COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetAllocations_Schedule",
                table: "BudgetAllocations",
                sql: "(\"ScheduleFrequency\" IS NULL AND \"ScheduleStartDate\" IS NULL AND \"ScheduleEndDate\" IS NULL\nAND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL) OR COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");
        }
    }
}
