using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleSectionSignoffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "ReviewWorkflowEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SampleSectionSignoffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedForReviewAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewSignatureId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalDecision = table.Column<int>(type: "integer", nullable: true),
                    ApprovalSignatureId = table.Column<int>(type: "integer", nullable: true),
                    CertificateRemarks = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleSectionSignoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleSectionSignoffs_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleSectionSignoffs_ElectronicSignatures_ApprovalSignatur~",
                        column: x => x.ApprovalSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleSectionSignoffs_ElectronicSignatures_ReviewSignatureId",
                        column: x => x.ReviewSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleSectionSignoffs_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleSectionSignoffs_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleSectionSignoffs_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewWorkflowEvents_SectionId",
                table: "ReviewWorkflowEvents",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_ApprovalSignatureId",
                table: "SampleSectionSignoffs",
                column: "ApprovalSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_ApprovedByUserId",
                table: "SampleSectionSignoffs",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_ReviewedByUserId",
                table: "SampleSectionSignoffs",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_ReviewSignatureId",
                table: "SampleSectionSignoffs",
                column: "ReviewSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_SampleId_SectionId",
                table: "SampleSectionSignoffs",
                columns: new[] { "SampleId", "SectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleSectionSignoffs_SectionId_Status",
                table: "SampleSectionSignoffs",
                columns: new[] { "SectionId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewWorkflowEvents_DocumentSections_SectionId",
                table: "ReviewWorkflowEvents",
                column: "SectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // One sign-off per (sample, section) for every sample that has
            // reached review or been closed. SampleStatus and
            // SectionSignoffStatus share their order from UnderReview
            // onwards, offset by one (SampleStatus has Received first).
            // Historical ReviewWorkflowEvents are audit history and are read
            // here, never rewritten - their SectionId stays null.
            migrationBuilder.Sql(@"
                INSERT INTO ""SampleSectionSignoffs""
                    (""SampleId"", ""SectionId"", ""Status"", ""SubmittedForReviewAt"",
                     ""ReviewedByUserId"", ""ReviewedAt"", ""ReviewSignatureId"",
                     ""ApprovedByUserId"", ""ApprovedAt"", ""ApprovalDecision"", ""ApprovalSignatureId"",
                     ""CertificateRemarks"")
                SELECT s.""Id"", sec.""SectionId"", s.""Status"" - 1,
                    (SELECT max(e.""Timestamp"") FROM ""ReviewWorkflowEvents"" e
                      WHERE e.""EntityType"" = 'Sample' AND e.""EntityId"" = s.""Id"" AND e.""EventType"" = 0),
                    s.""ReviewedByUserId"", s.""ReviewedAt"",
                    (SELECT max(g.""Id"") FROM ""ElectronicSignatures"" g
                      WHERE g.""EntityType"" = 'Sample' AND g.""EntityId"" = s.""Id"" AND g.""MeaningOfSignature"" = 0),
                    s.""ApprovedByUserId"", s.""ApprovedAt"", s.""ApprovalDecision"",
                    (SELECT max(g.""Id"") FROM ""ElectronicSignatures"" g
                      WHERE g.""EntityType"" = 'Sample' AND g.""EntityId"" = s.""Id"" AND g.""MeaningOfSignature"" IN (1, 2, 3)),
                    s.""CertificateRemarks""
                FROM ""Samples"" s
                JOIN (SELECT DISTINCT ""SampleId"", ""SectionId"" FROM ""TestOrders"") sec ON sec.""SampleId"" = s.""Id""
                WHERE s.""Status"" >= 2;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewWorkflowEvents_DocumentSections_SectionId",
                table: "ReviewWorkflowEvents");

            migrationBuilder.DropTable(
                name: "SampleSectionSignoffs");

            migrationBuilder.DropIndex(
                name: "IX_ReviewWorkflowEvents_SectionId",
                table: "ReviewWorkflowEvents");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "ReviewWorkflowEvents");
        }
    }
}
