using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAcknowledgementRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentAcknowledgementRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentTrainingAssignmentId = table.Column<int>(type: "integer", nullable: false),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StatementText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ControlledFileId = table.Column<int>(type: "integer", nullable: true),
                    ControlledFileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Comments = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientIpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAcknowledgementRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAcknowledgementRecords_DocumentMasters_DocumentMasterId",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentAcknowledgementRecords_DocumentRevisions_DocumentRevisionId",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentAcknowledgementRecords_DocumentTrainingAssignments_DocumentTrainingAssignmentId",
                        column: x => x.DocumentTrainingAssignmentId,
                        principalTable: "DocumentTrainingAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentAcknowledgementRecords_RevisionFiles_ControlledFileId",
                        column: x => x.ControlledFileId,
                        principalTable: "RevisionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentAcknowledgementRecords_Users_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAcknowledgementRecords_AcknowledgedAtUtc",
                table: "DocumentAcknowledgementRecords",
                column: "AcknowledgedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAcknowledgementRecords_AcknowledgedByUserId",
                table: "DocumentAcknowledgementRecords",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAcknowledgementRecords_ControlledFileId",
                table: "DocumentAcknowledgementRecords",
                column: "ControlledFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAcknowledgementRecords_DocumentMasterId_AcknowledgedByUserId",
                table: "DocumentAcknowledgementRecords",
                columns: new[] { "DocumentMasterId", "AcknowledgedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAcknowledgementRecords_DocumentRevisionId_AcknowledgedByUserId",
                table: "DocumentAcknowledgementRecords",
                columns: new[] { "DocumentRevisionId", "AcknowledgedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAcknowledgementRecords_DocumentTrainingAssignmentId",
                table: "DocumentAcknowledgementRecords",
                column: "DocumentTrainingAssignmentId",
                unique: true);

            // Database-Level Immutability: Attach append-only trigger to prevent UPDATE and DELETE
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_documentacknowledgementrecords_immutable
BEFORE UPDATE OR DELETE ON ""DocumentAcknowledgementRecords""
FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_documentacknowledgementrecords_immutable ON ""DocumentAcknowledgementRecords"";
");
            migrationBuilder.DropTable(
                name: "DocumentAcknowledgementRecords");
        }
    }
}
