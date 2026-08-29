using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOosGroupingAndInvestigationDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OosGroupCode",
                table: "Samples",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OosInvestigationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OosGroupCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SupersededByDocumentId = table.Column<int>(type: "integer", nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SupersededByUserId = table.Column<int>(type: "integer", nullable: true),
                    SupersessionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OosInvestigationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OosInvestigationDocuments_OosInvestigationDocuments_Superse~",
                        column: x => x.SupersededByDocumentId,
                        principalTable: "OosInvestigationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OosInvestigationDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_OosGroupCode",
                table: "Samples",
                column: "OosGroupCode");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationDocuments_OosGroupCode",
                table: "OosInvestigationDocuments",
                column: "OosGroupCode");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationDocuments_OosGroupCode_Status",
                table: "OosInvestigationDocuments",
                columns: new[] { "OosGroupCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationDocuments_SupersededByDocumentId",
                table: "OosInvestigationDocuments",
                column: "SupersededByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_OosInvestigationDocuments_UploadedByUserId",
                table: "OosInvestigationDocuments",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OosInvestigationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Samples_OosGroupCode",
                table: "Samples");

            migrationBuilder.DropColumn(
                name: "OosGroupCode",
                table: "Samples");
        }
    }
}
