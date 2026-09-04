using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentApprovalTaskEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentApprovalTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    AssignedApproverUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SubmissionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TargetEffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentApprovalTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalTasks_DocumentRevisions_DocumentRevisionId",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalTasks_Users_AssignedApproverUserId",
                        column: x => x.AssignedApproverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalTasks_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentApprovalTasks_Users_DecisionByUserId",
                        column: x => x.DecisionByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalTasks_AssignedApproverUserId",
                table: "DocumentApprovalTasks",
                column: "AssignedApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalTasks_AssignedByUserId",
                table: "DocumentApprovalTasks",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalTasks_DecisionByUserId",
                table: "DocumentApprovalTasks",
                column: "DecisionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalTasks_DocumentRevisionId",
                table: "DocumentApprovalTasks",
                column: "DocumentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalTasks_Status",
                table: "DocumentApprovalTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentApprovalTasks");
        }
    }
}
