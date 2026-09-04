using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodicReviewEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeriodicReviewTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    ReviewCycleMonths = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedReviewerUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodicReviewTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodicReviewTasks_DocumentMasters_DocumentMasterId",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodicReviewTasks_DocumentRevisions_DocumentRevisionId",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodicReviewTasks_Users_AssignedReviewerUserId",
                        column: x => x.AssignedReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodicReviewTasks_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodicReviewFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PeriodicReviewTaskId = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    SectionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NoteText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodicReviewFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodicReviewFindings_PeriodicReviewTasks_PeriodicReviewTa~",
                        column: x => x.PeriodicReviewTaskId,
                        principalTable: "PeriodicReviewTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeriodicReviewFindings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewFindings_CreatedByUserId",
                table: "PeriodicReviewFindings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewFindings_PeriodicReviewTaskId",
                table: "PeriodicReviewFindings",
                column: "PeriodicReviewTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewFindings_Status",
                table: "PeriodicReviewFindings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewTasks_AssignedReviewerUserId",
                table: "PeriodicReviewTasks",
                column: "AssignedReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewTasks_CompletedByUserId",
                table: "PeriodicReviewTasks",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewTasks_DocumentMasterId",
                table: "PeriodicReviewTasks",
                column: "DocumentMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewTasks_DocumentRevisionId",
                table: "PeriodicReviewTasks",
                column: "DocumentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewTasks_ScheduledDueDate",
                table: "PeriodicReviewTasks",
                column: "ScheduledDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodicReviewTasks_Status",
                table: "PeriodicReviewTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PeriodicReviewFindings");

            migrationBuilder.DropTable(
                name: "PeriodicReviewTasks");
        }
    }
}
