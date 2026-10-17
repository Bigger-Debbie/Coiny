using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coiny.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdJoinInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers");

            migrationBuilder.CreateTable(
                name: "HouseholdInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CodeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    ConsumedAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    RevokedAtUtcTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvitations", x => x.Id);
                    table.CheckConstraint("CK_HouseholdInvitations_CodeHash", "length(\"CodeHash\") = 64");
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_CodeHash",
                table: "HouseholdInvitations",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_CreatedByUserId",
                table: "HouseholdInvitations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId",
                table: "HouseholdInvitations",
                column: "HouseholdId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdInvitations");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_UserId",
                table: "HouseholdMembers",
                column: "UserId");
        }
    }
}
