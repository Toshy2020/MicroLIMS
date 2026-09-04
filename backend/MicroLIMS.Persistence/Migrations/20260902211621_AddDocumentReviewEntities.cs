using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentReviewEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentReviewTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    AssignedReviewerUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SubmissionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentReviewTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentReviewTasks_DocumentRevisions_DocumentRevisionId",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentReviewTasks_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentReviewTasks_Users_AssignedReviewerUserId",
                        column: x => x.AssignedReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentReviewTasks_Users_DecisionByUserId",
                        column: x => x.DecisionByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentReviewFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentReviewTaskId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    SectionNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CommentText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AuthorResponse = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AuthorResponseAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthorResponseByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewerVerificationNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ReviewerVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerVerifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentReviewFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentReviewFindings_DocumentReviewTasks_DocumentReviewTa~",
                        column: x => x.DocumentReviewTaskId,
                        principalTable: "DocumentReviewTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentReviewFindings_Users_AuthorResponseByUserId",
                        column: x => x.AuthorResponseByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentReviewFindings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentReviewFindings_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentReviewFindings_Users_ReviewerVerifiedByUserId",
                        column: x => x.ReviewerVerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_AuthorResponseByUserId",
                table: "DocumentReviewFindings",
                column: "AuthorResponseByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_CreatedByUserId",
                table: "DocumentReviewFindings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_DocumentReviewTaskId",
                table: "DocumentReviewFindings",
                column: "DocumentReviewTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_IsMandatory",
                table: "DocumentReviewFindings",
                column: "IsMandatory");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_ResolvedByUserId",
                table: "DocumentReviewFindings",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_ReviewerVerifiedByUserId",
                table: "DocumentReviewFindings",
                column: "ReviewerVerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_Status",
                table: "DocumentReviewFindings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewTasks_AssignedByUserId",
                table: "DocumentReviewTasks",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewTasks_AssignedReviewerUserId",
                table: "DocumentReviewTasks",
                column: "AssignedReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewTasks_DecisionByUserId",
                table: "DocumentReviewTasks",
                column: "DecisionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewTasks_DocumentRevisionId",
                table: "DocumentReviewTasks",
                column: "DocumentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewTasks_Status",
                table: "DocumentReviewTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentReviewFindings");

            migrationBuilder.DropTable(
                name: "DocumentReviewTasks");
        }
    }
}
