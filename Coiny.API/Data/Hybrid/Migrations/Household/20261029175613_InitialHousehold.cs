using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Hybrid.Migrations.Household
{
    /// <inheritdoc />
    public partial class InitialHousehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CategoryType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

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
                });

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
                });

            migrationBuilder.CreateTable(
                name: "HouseholdDatabaseMetadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdDatabaseMetadata", x => x.Id);
                    table.CheckConstraint("CK_HouseholdDatabaseMetadata_Singleton", "Id = 1");
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
                });

            migrationBuilder.CreateTable(
                name: "Institutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Website = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringExpenseReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    PatternKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Dismissed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringExpenseReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionImportSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionImportSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionImportUndo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    BatchIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionImportUndo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetCategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveFromMonth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetCategoryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetCategoryRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MerchantCategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedPattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantCategoryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantCategoryRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
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
                        name: "FK_HouseholdAlertReads_HouseholdAlerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "HouseholdAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdBudgetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveFromMonth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EffectiveFromDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ProtectedPaymentsJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    SetupStep = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetRevisions", x => x.Id);
                    table.CheckConstraint("CK_BudgetRevisions_EditVersion", "\"EditVersion\" >= 0");
                    table.CheckConstraint("CK_BudgetRevisions_EffectiveMonth", "substr(\"EffectiveFromMonth\", 9, 2) = '01'");
                    table.CheckConstraint("CK_BudgetRevisions_RevisionNumber", "\"RevisionNumber\" > 0");
                    table.CheckConstraint("CK_BudgetRevisions_SetupStep", "\"SetupStep\" BETWEEN 0 AND 7");
                    table.CheckConstraint("CK_BudgetRevisions_State", "(\"State\" = 0 AND \"PublishedAt\" IS NULL) OR (\"State\" = 1 AND \"PublishedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_BudgetRevisions_HouseholdBudgets_HouseholdBudgetId",
                        column: x => x.HouseholdBudgetId,
                        principalTable: "HouseholdBudgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    InstitutionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    LoanSubtype = table.Column<int>(type: "INTEGER", nullable: true),
                    AccountType = table.Column<int>(type: "INTEGER", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    OpeningBalanceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.CheckConstraint("CK_Accounts_LoanSubtype", "LoanSubtype IS NULL OR (AccountType = 5 AND LoanSubtype BETWEEN 0 AND 4)");
                    table.ForeignKey(
                        name: "FK_Accounts_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id");
                });

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
                        name: "FK_SavedCsvMappings_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BudgetAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    BudgetRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AllocationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SavingsPlanJson = table.Column<string>(type: "TEXT", nullable: true),
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
                    table.CheckConstraint("CK_BudgetAllocations_Schedule", "(\"ScheduleFrequency\" IS NULL AND \"ScheduleStartDate\" IS NULL AND \"ScheduleEndDate\" IS NULL\nAND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL) OR COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1, 4, 5) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");
                    table.CheckConstraint("CK_BudgetAllocations_Type", "\"AllocationType\" BETWEEN 0 AND 5");
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
                    SourceKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    BudgetRevisionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    HealthInsurancePerPaycheck = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    RetirementContributionPerPaycheck = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
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
                    table.CheckConstraint("CK_BudgetIncomeSources_HealthInsurance", "\"HealthInsurancePerPaycheck\" IS NULL OR CAST(\"HealthInsurancePerPaycheck\" AS REAL) >= 0");
                    table.CheckConstraint("CK_BudgetIncomeSources_Name", "length(trim(\"Name\")) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_BudgetIncomeSources_RetirementContribution", "\"RetirementContributionPerPaycheck\" IS NULL OR CAST(\"RetirementContributionPerPaycheck\" AS REAL) >= 0");
                    table.CheckConstraint("CK_BudgetIncomeSources_Schedule", "COALESCE((\"ScheduleFrequency\" IS NOT NULL AND \"ScheduleStartDate\" IS NOT NULL\nAND (\"ScheduleEndDate\" IS NULL OR \"ScheduleEndDate\" >= \"ScheduleStartDate\")\nAND (\n    (\"ScheduleFrequency\" IN (0, 1, 4, 5) AND \"ScheduleDayOfMonth\" IS NULL AND \"ScheduleSecondDayOfMonth\" IS NULL)\n    OR (\"ScheduleFrequency\" = 2 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31\n        AND \"ScheduleSecondDayOfMonth\" > \"ScheduleDayOfMonth\" AND \"ScheduleSecondDayOfMonth\" <= 31)\n    OR (\"ScheduleFrequency\" = 3 AND \"ScheduleDayOfMonth\" BETWEEN 1 AND 31 AND \"ScheduleSecondDayOfMonth\" IS NULL)\n)), 0)");
                    table.ForeignKey(
                        name: "FK_BudgetIncomeSources_BudgetRevisions_BudgetRevisionId",
                        column: x => x.BudgetRevisionId,
                        principalTable: "BudgetRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountBalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBalanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountBalanceSnapshots_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountReconciliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StatementBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LedgerFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConfirmedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ConfirmedByName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    InvalidatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InvalidationReason = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountReconciliations_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavingsFund",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PlanJson = table.Column<string>(type: "TEXT", nullable: false),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsFund", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsFund_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Filename = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ImportedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalStateJson = table.Column<string>(type: "TEXT", nullable: false),
                    UndoneCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUndoneAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionImportBatches_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EditVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Merchant = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsCleared = table.Column<bool>(type: "INTEGER", nullable: false),
                    TransferDirection = table.Column<int>(type: "INTEGER", nullable: true),
                    TransferCounterparty = table.Column<int>(type: "INTEGER", nullable: true),
                    PairedTransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportBatchId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImportRowId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.CheckConstraint("CK_Transaction_TransferMetadata", "(TransferDirection IS NULL AND TransferCounterparty IS NULL AND PairedTransactionId IS NULL) OR (TransferDirection IS NOT NULL AND TransferCounterparty IS NOT NULL AND TransferDirection IN (0,1) AND TransferCounterparty IN (0,1) AND PairedTransactionId IS NULL) OR (TransferDirection IS NOT NULL AND TransferCounterparty IS NOT NULL AND TransferDirection IN (0,1) AND TransferCounterparty = 2 AND PairedTransactionId IS NOT NULL AND PairedTransactionId <> Id)");
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_TransactionImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "TransactionImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Transactions_PairedTransactionId",
                        column: x => x.PairedTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsSkipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    SkippedPeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SkippedPeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    IsPaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillPayments_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SavingsActivity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SavingsFundId = table.Column<int>(type: "INTEGER", nullable: false),
                    ComponentKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    OperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsActivity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsActivity_SavingsFund_SavingsFundId",
                        column: x => x.SavingsFundId,
                        principalTable: "SavingsFund",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavingsActivity_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TransactionBudgetAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AllocationKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionBudgetAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionBudgetAssignments_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_AccountBalanceSnapshots_AccountId_SnapshotDate",
                table: "AccountBalanceSnapshots",
                columns: new[] { "AccountId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountReconciliations_AccountId_StatementDate_LedgerFingerprint",
                table: "AccountReconciliations",
                columns: new[] { "AccountId", "StatementDate", "LedgerFingerprint" },
                unique: true,
                filter: "\"InvalidatedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_InstitutionId",
                table: "Accounts",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_HouseholdId_AllocationKey_DueDate",
                table: "BillPayments",
                columns: new[] { "HouseholdId", "AllocationKey", "DueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillPayments_TransactionId",
                table: "BillPayments",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillPaymentTransaction_BillPaymentId",
                table: "BillPaymentTransaction",
                column: "BillPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_BillPaymentTransaction_TransactionId",
                table: "BillPaymentTransaction",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAllocations_BudgetRevisionId_SourceKey",
                table: "BudgetAllocations",
                columns: new[] { "BudgetRevisionId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategoryRules_CategoryId",
                table: "BudgetCategoryRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetCategoryRules_HouseholdId_CategoryId_EffectiveFromMonth",
                table: "BudgetCategoryRules",
                columns: new[] { "HouseholdId", "CategoryId", "EffectiveFromMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetIncomeSources_BudgetRevisionId_SourceKey",
                table: "BudgetIncomeSources",
                columns: new[] { "BudgetRevisionId", "SourceKey" },
                unique: true);

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
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAlerts_HouseholdId_PatternKey",
                table: "HouseholdAlerts",
                columns: new[] { "HouseholdId", "PatternKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdBudgets_HouseholdId",
                table: "HouseholdBudgets",
                column: "HouseholdId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdRecurringSettings_HouseholdId",
                table: "HouseholdRecurringSettings",
                column: "HouseholdId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantCategoryRules_CategoryId",
                table: "MerchantCategoryRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantCategoryRules_HouseholdId_MatchType_NormalizedPattern",
                table: "MerchantCategoryRules",
                columns: new[] { "HouseholdId", "MatchType", "NormalizedPattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenseReviews_HouseholdId_PatternKey",
                table: "RecurringExpenseReviews",
                columns: new[] { "HouseholdId", "PatternKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedCsvMappings_HouseholdId_NormalizedName",
                table: "SavedCsvMappings",
                columns: new[] { "HouseholdId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedCsvMappings_InstitutionId",
                table: "SavedCsvMappings",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsActivity_SavingsFundId_OperationId_ComponentKey",
                table: "SavingsActivity",
                columns: new[] { "SavingsFundId", "OperationId", "ComponentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsActivity_TransactionId",
                table: "SavingsActivity",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsFund_AccountId",
                table: "SavingsFund",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsFund_HouseholdId_SourceKey",
                table: "SavingsFund",
                columns: new[] { "HouseholdId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionBudgetAssignments_TransactionId",
                table: "TransactionBudgetAssignments",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionImportBatches_AccountId",
                table: "TransactionImportBatches",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionImportBatches_HouseholdId_SubmissionId",
                table: "TransactionImportBatches",
                columns: new[] { "HouseholdId", "SubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionImportSessions_HouseholdId_SubmissionId",
                table: "TransactionImportSessions",
                columns: new[] { "HouseholdId", "SubmissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionImportUndo_HouseholdId_OperationId",
                table: "TransactionImportUndo",
                columns: new[] { "HouseholdId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportBatchId_ImportRowId",
                table: "Transactions",
                columns: new[] { "ImportBatchId", "ImportRowId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PairedTransactionId",
                table: "Transactions",
                column: "PairedTransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountBalanceSnapshots");

            migrationBuilder.DropTable(
                name: "AccountReconciliations");

            migrationBuilder.DropTable(
                name: "BillPaymentTransaction");

            migrationBuilder.DropTable(
                name: "BudgetAllocations");

            migrationBuilder.DropTable(
                name: "BudgetCategoryRules");

            migrationBuilder.DropTable(
                name: "BudgetIncomeSources");

            migrationBuilder.DropTable(
                name: "HouseholdAlertReads");

            migrationBuilder.DropTable(
                name: "HouseholdDatabaseMetadata");

            migrationBuilder.DropTable(
                name: "HouseholdRecurringSettings");

            migrationBuilder.DropTable(
                name: "MerchantCategoryRules");

            migrationBuilder.DropTable(
                name: "RecurringExpenseReviews");

            migrationBuilder.DropTable(
                name: "SavedCsvMappings");

            migrationBuilder.DropTable(
                name: "SavingsActivity");

            migrationBuilder.DropTable(
                name: "TransactionBudgetAssignments");

            migrationBuilder.DropTable(
                name: "TransactionImportSessions");

            migrationBuilder.DropTable(
                name: "TransactionImportUndo");

            migrationBuilder.DropTable(
                name: "BillPayments");

            migrationBuilder.DropTable(
                name: "BudgetRevisions");

            migrationBuilder.DropTable(
                name: "HouseholdAlerts");

            migrationBuilder.DropTable(
                name: "SavingsFund");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "HouseholdBudgets");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "TransactionImportBatches");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Institutions");
        }
    }
}
