using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRelease1dDocumentLifecycleLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FileVersion",
                table: "RevisionFiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "GeneratedFromSourceFileId",
                table: "RevisionFiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApprovedFinalSource",
                table: "RevisionFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedSourceFileId",
                table: "DocumentRevisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ControlledPdfFileId",
                table: "DocumentRevisions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCycleNumber",
                table: "DocumentReviewTasks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedSourceFileId",
                table: "DocumentReviewTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevisionFileId",
                table: "DocumentReviewFindings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceFileVersion",
                table: "DocumentReviewFindings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedSourceFileId",
                table: "DocumentApprovalTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GeneratedControlledPdfId",
                table: "DocumentApprovalTasks",
                type: "integer",
                nullable: true);

            // Release 1d: Deterministic backfill of FileVersion for existing RevisionFiles
            // Ensures existing rows satisfy the unique constraint (DocumentRevisionId, FileRole, FileVersion)
            migrationBuilder.Sql(@"
                WITH numbered AS (
                    SELECT ""Id"", ROW_NUMBER() OVER (
                        PARTITION BY ""DocumentRevisionId"", ""FileRole""
                        ORDER BY ""UploadedAt"" ASC, ""Id"" ASC
                    ) AS calculated_version
                    FROM ""RevisionFiles""
                )
                UPDATE ""RevisionFiles"" rf
                SET ""FileVersion"" = n.calculated_version
                FROM numbered n
                WHERE rf.""Id"" = n.""Id"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFiles_DocumentRevisionId_FileRole_FileVersion",
                table: "RevisionFiles",
                columns: new[] { "DocumentRevisionId", "FileRole", "FileVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFiles_DocumentRevisionId_IsApprovedFinalSource",
                table: "RevisionFiles",
                columns: new[] { "DocumentRevisionId", "IsApprovedFinalSource" },
                unique: true,
                filter: "\"IsApprovedFinalSource\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFiles_GeneratedFromSourceFileId",
                table: "RevisionFiles",
                column: "GeneratedFromSourceFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevisionFiles_ApprovedFinalSourceRole",
                table: "RevisionFiles",
                sql: "(\"IsApprovedFinalSource\" = false) OR (\"IsApprovedFinalSource\" = true AND \"FileRole\" = 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevisionFiles_GeneratedFromSourceRole",
                table: "RevisionFiles",
                sql: "(\"GeneratedFromSourceFileId\" IS NULL) OR (\"FileRole\" = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RevisionFiles_NotSelfGenerated",
                table: "RevisionFiles",
                sql: "(\"GeneratedFromSourceFileId\" IS NULL) OR (\"GeneratedFromSourceFileId\" <> \"Id\")");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_ApprovedSourceFileId",
                table: "DocumentRevisions",
                column: "ApprovedSourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_ControlledPdfFileId",
                table: "DocumentRevisions",
                column: "ControlledPdfFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewTasks_DocumentRevisionId_ReviewCycleNumber",
                table: "DocumentReviewTasks",
                columns: new[] { "DocumentRevisionId", "ReviewCycleNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewTasks_ReviewedSourceFileId",
                table: "DocumentReviewTasks",
                column: "ReviewedSourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewFindings_RevisionFileId",
                table: "DocumentReviewFindings",
                column: "RevisionFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalTasks_ApprovedSourceFileId",
                table: "DocumentApprovalTasks",
                column: "ApprovedSourceFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentApprovalTasks_GeneratedControlledPdfId",
                table: "DocumentApprovalTasks",
                column: "GeneratedControlledPdfId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentApprovalTasks_RevisionFiles_ApprovedSourceFileId",
                table: "DocumentApprovalTasks",
                column: "ApprovedSourceFileId",
                principalTable: "RevisionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentApprovalTasks_RevisionFiles_GeneratedControlledPdfId",
                table: "DocumentApprovalTasks",
                column: "GeneratedControlledPdfId",
                principalTable: "RevisionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentReviewFindings_RevisionFiles_RevisionFileId",
                table: "DocumentReviewFindings",
                column: "RevisionFileId",
                principalTable: "RevisionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentReviewTasks_RevisionFiles_ReviewedSourceFileId",
                table: "DocumentReviewTasks",
                column: "ReviewedSourceFileId",
                principalTable: "RevisionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRevisions_RevisionFiles_ApprovedSourceFileId",
                table: "DocumentRevisions",
                column: "ApprovedSourceFileId",
                principalTable: "RevisionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRevisions_RevisionFiles_ControlledPdfFileId",
                table: "DocumentRevisions",
                column: "ControlledPdfFileId",
                principalTable: "RevisionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RevisionFiles_RevisionFiles_GeneratedFromSourceFileId",
                table: "RevisionFiles",
                column: "GeneratedFromSourceFileId",
                principalTable: "RevisionFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentApprovalTasks_RevisionFiles_ApprovedSourceFileId",
                table: "DocumentApprovalTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentApprovalTasks_RevisionFiles_GeneratedControlledPdfId",
                table: "DocumentApprovalTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentReviewFindings_RevisionFiles_RevisionFileId",
                table: "DocumentReviewFindings");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentReviewTasks_RevisionFiles_ReviewedSourceFileId",
                table: "DocumentReviewTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRevisions_RevisionFiles_ApprovedSourceFileId",
                table: "DocumentRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRevisions_RevisionFiles_ControlledPdfFileId",
                table: "DocumentRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_RevisionFiles_RevisionFiles_GeneratedFromSourceFileId",
                table: "RevisionFiles");

            migrationBuilder.DropIndex(
                name: "IX_RevisionFiles_DocumentRevisionId_FileRole_FileVersion",
                table: "RevisionFiles");

            migrationBuilder.DropIndex(
                name: "IX_RevisionFiles_DocumentRevisionId_IsApprovedFinalSource",
                table: "RevisionFiles");

            migrationBuilder.DropIndex(
                name: "IX_RevisionFiles_GeneratedFromSourceFileId",
                table: "RevisionFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevisionFiles_ApprovedFinalSourceRole",
                table: "RevisionFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevisionFiles_GeneratedFromSourceRole",
                table: "RevisionFiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RevisionFiles_NotSelfGenerated",
                table: "RevisionFiles");

            migrationBuilder.DropIndex(
                name: "IX_DocumentRevisions_ApprovedSourceFileId",
                table: "DocumentRevisions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentRevisions_ControlledPdfFileId",
                table: "DocumentRevisions");

            migrationBuilder.DropIndex(
                name: "IX_DocumentReviewTasks_DocumentRevisionId_ReviewCycleNumber",
                table: "DocumentReviewTasks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentReviewTasks_ReviewedSourceFileId",
                table: "DocumentReviewTasks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentReviewFindings_RevisionFileId",
                table: "DocumentReviewFindings");

            migrationBuilder.DropIndex(
                name: "IX_DocumentApprovalTasks_ApprovedSourceFileId",
                table: "DocumentApprovalTasks");

            migrationBuilder.DropIndex(
                name: "IX_DocumentApprovalTasks_GeneratedControlledPdfId",
                table: "DocumentApprovalTasks");

            migrationBuilder.DropColumn(
                name: "FileVersion",
                table: "RevisionFiles");

            migrationBuilder.DropColumn(
                name: "GeneratedFromSourceFileId",
                table: "RevisionFiles");

            migrationBuilder.DropColumn(
                name: "IsApprovedFinalSource",
                table: "RevisionFiles");

            migrationBuilder.DropColumn(
                name: "ApprovedSourceFileId",
                table: "DocumentRevisions");

            migrationBuilder.DropColumn(
                name: "ControlledPdfFileId",
                table: "DocumentRevisions");

            migrationBuilder.DropColumn(
                name: "ReviewCycleNumber",
                table: "DocumentReviewTasks");

            migrationBuilder.DropColumn(
                name: "ReviewedSourceFileId",
                table: "DocumentReviewTasks");

            migrationBuilder.DropColumn(
                name: "RevisionFileId",
                table: "DocumentReviewFindings");

            migrationBuilder.DropColumn(
                name: "SourceFileVersion",
                table: "DocumentReviewFindings");

            migrationBuilder.DropColumn(
                name: "ApprovedSourceFileId",
                table: "DocumentApprovalTasks");

            migrationBuilder.DropColumn(
                name: "GeneratedControlledPdfId",
                table: "DocumentApprovalTasks");
        }
    }
}
