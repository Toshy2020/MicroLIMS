using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SampleLevelReviewAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SampleStatus.UnderApproval was inserted between UnderReview
            // and Approved in the C# enum, but Npgsql stores this enum as
            // a plain integer column (ordinal), not a Postgres enum type -
            // so every value from the old Approved(3) onward shifts up by
            // one. Any Sample already sitting at Approved/Rejected/
            // RetestRequested must be remapped or it silently becomes a
            // different status after this migration. Processed highest
            // source value first so no statement's WHERE clause re-catches
            // rows a prior statement in this same block just wrote.
            migrationBuilder.Sql(@"UPDATE ""Samples"" SET ""Status"" = 6 WHERE ""Status"" = 5;"); // RetestRequested: 5 -> 6
            migrationBuilder.Sql(@"UPDATE ""Samples"" SET ""Status"" = 5 WHERE ""Status"" = 4;"); // Rejected: 4 -> 5
            migrationBuilder.Sql(@"UPDATE ""Samples"" SET ""Status"" = 4 WHERE ""Status"" = 3;"); // Approved: 3 -> 4

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperseded",
                table: "TestOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalDecision",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Samples",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "Samples",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "Samples",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SampleWorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PerformedByNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleWorkflowEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleWorkflowEvents_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_ApprovedByUserId",
                table: "Samples",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Samples_ReviewedByUserId",
                table: "Samples",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleWorkflowEvents_SampleId",
                table: "SampleWorkflowEvents",
                column: "SampleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_Users_ApprovedByUserId",
                table: "Samples",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Samples_Users_ReviewedByUserId",
                table: "Samples",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Samples_Users_ApprovedByUserId",
                table: "Samples");

            migrationBuilder.DropForeignKey(
                name: "FK_Samples_Users_ReviewedByUserId",
                table: "Samples");

            migrationBuilder.DropTable(
                name: "SampleWorkflowEvents");

            migrationBuilder.DropIndex(
                name: "IX_Samples_ApprovedByUserId",
                table: "Samples");

            migrationBuilder.DropIndex(
                name: "IX_Samples_ReviewedByUserId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "IsSuperseded",
                table: "TestOrders");

            migrationBuilder.DropColumn(
                name: "ApprovalDecision",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "Samples");

            // Reverse the Status remap from Up() - ascending source order
            // this time, for the same no-double-shift reason.
            migrationBuilder.Sql(@"UPDATE ""Samples"" SET ""Status"" = 3 WHERE ""Status"" = 4;"); // Approved: 4 -> 3
            migrationBuilder.Sql(@"UPDATE ""Samples"" SET ""Status"" = 4 WHERE ""Status"" = 5;"); // Rejected: 5 -> 4
            migrationBuilder.Sql(@"UPDATE ""Samples"" SET ""Status"" = 5 WHERE ""Status"" = 6;"); // RetestRequested: 6 -> 5
        }
    }
}
