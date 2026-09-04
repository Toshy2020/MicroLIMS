using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentEscalationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentEscalationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentTrainingAssignmentId = table.Column<int>(type: "integer", nullable: false),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    AssignedUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentType = table.Column<int>(type: "integer", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledTriggerUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecipientRoleOrTarget = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EscalationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolutionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProcessName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentEscalationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentEscalationRecords_DocumentMasters_DocumentMasterId",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentEscalationRecords_DocumentRevisions_DocumentRevisionId",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentEscalationRecords_DocumentTrainingAssignments_DocumentTrainingAssignmentId",
                        column: x => x.DocumentTrainingAssignmentId,
                        principalTable: "DocumentTrainingAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentEscalationRecords_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentEscalationRecords_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_AssignedUserId",
                table: "DocumentEscalationRecords",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_DocumentMasterId",
                table: "DocumentEscalationRecords",
                column: "DocumentMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_DocumentRevisionId",
                table: "DocumentEscalationRecords",
                column: "DocumentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_DocumentTrainingAssignmentId",
                table: "DocumentEscalationRecords",
                column: "DocumentTrainingAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_EscalationLevel",
                table: "DocumentEscalationRecords",
                column: "EscalationLevel");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_ExecutedAtUtc",
                table: "DocumentEscalationRecords",
                column: "ExecutedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_Status",
                table: "DocumentEscalationRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentEscalationRecords_DocumentTrainingAssignmentId_EscalationLevel",
                table: "DocumentEscalationRecords",
                columns: new[] { "DocumentTrainingAssignmentId", "EscalationLevel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentEscalationRecords");
        }
    }
}
