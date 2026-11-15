using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionSelfAssignmentSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectionSelfAssignmentSettings",
                columns: table => new
                {
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionSelfAssignmentSettings", x => x.SectionId);
                    table.ForeignKey(
                        name: "FK_SectionSelfAssignmentSettings_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SectionSelfAssignmentSettings_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectionSelfAssignmentSettings_ChangedByUserId",
                table: "SectionSelfAssignmentSettings",
                column: "ChangedByUserId");

            migrationBuilder.Sql(@"
                INSERT INTO ""SectionSelfAssignmentSettings"" (""SectionId"", ""Enabled"", ""ChangedAt"", ""ChangedByUserId"")
                SELECT s.""Id"", FALSE, now(), NULL
                FROM ""DocumentSections"" AS s
                WHERE EXISTS (
                    SELECT 1 FROM ""SystemSettings"" AS ss
                    WHERE ss.""Key"" = 'workflow.selfAssignment.enabled'
                      AND LOWER(TRIM(ss.""Value"")) = 'false'
                )
                AND NOT EXISTS (
                    SELECT 1 FROM ""SectionSelfAssignmentSettings"" AS existing
                    WHERE existing.""SectionId"" = s.""Id""
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionSelfAssignmentSettings");
        }
    }
}
