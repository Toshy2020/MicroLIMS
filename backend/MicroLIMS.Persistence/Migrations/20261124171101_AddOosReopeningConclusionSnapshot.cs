using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOosReopeningConclusionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PreviousConcludedAtUtc",
                table: "OosInvestigationReopenings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousConcludedByUserId",
                table: "OosInvestigationReopenings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousConclusionSummary",
                table: "OosInvestigationReopenings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousRootCauseCategoryId",
                table: "OosInvestigationReopenings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousRootCauseDescription",
                table: "OosInvestigationReopenings",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationReopenings_PreviousConcludedByUserId",
                table: "OosInvestigationReopenings",
                column: "PreviousConcludedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationReopenings_PreviousRootCauseCategoryId",
                table: "OosInvestigationReopenings",
                column: "PreviousRootCauseCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_OosInvestigationReopenings_OosCauseCategories_PreviousRootC~",
                table: "OosInvestigationReopenings",
                column: "PreviousRootCauseCategoryId",
                principalTable: "OosCauseCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OosInvestigationReopenings_Users_PreviousConcludedByUserId",
                table: "OosInvestigationReopenings",
                column: "PreviousConcludedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "OosInvestigationReopenings"
                        WHERE "PreviousConcludedAtUtc" IS NOT NULL
                           OR "PreviousConcludedByUserId" IS NOT NULL
                           OR "PreviousConclusionSummary" IS NOT NULL
                           OR "PreviousRootCauseCategoryId" IS NOT NULL
                           OR "PreviousRootCauseDescription" IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot remove stored signed OOS conclusion snapshots.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_OosInvestigationReopenings_OosCauseCategories_PreviousRootC~",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropForeignKey(
                name: "FK_OosInvestigationReopenings_Users_PreviousConcludedByUserId",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropIndex(
                name: "IX_OosInvestigationReopenings_PreviousConcludedByUserId",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropIndex(
                name: "IX_OosInvestigationReopenings_PreviousRootCauseCategoryId",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropColumn(
                name: "PreviousConcludedAtUtc",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropColumn(
                name: "PreviousConcludedByUserId",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropColumn(
                name: "PreviousConclusionSummary",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropColumn(
                name: "PreviousRootCauseCategoryId",
                table: "OosInvestigationReopenings");

            migrationBuilder.DropColumn(
                name: "PreviousRootCauseDescription",
                table: "OosInvestigationReopenings");
        }
    }
}
