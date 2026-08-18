using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaterialDocumentAccessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialDocumentAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_MaterialDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialDocuments_MaterialDocuments_SupersededByDocumentId",
                        column: x => x.SupersededByDocumentId,
                        principalTable: "MaterialDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialDocuments_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocumentAccessLogs_DocumentId",
                table: "MaterialDocumentAccessLogs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocumentAccessLogs_MaterialId",
                table: "MaterialDocumentAccessLogs",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocumentAccessLogs_UserId",
                table: "MaterialDocumentAccessLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocuments_MaterialId",
                table: "MaterialDocuments",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocuments_MaterialId_DocumentType_Status",
                table: "MaterialDocuments",
                columns: new[] { "MaterialId", "DocumentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocuments_MaterialId_Status",
                table: "MaterialDocuments",
                columns: new[] { "MaterialId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocuments_SupersededByDocumentId",
                table: "MaterialDocuments",
                column: "SupersededByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDocuments_UploadedByUserId",
                table: "MaterialDocuments",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialDocumentAccessLogs");

            migrationBuilder.DropTable(
                name: "MaterialDocuments");
        }
    }
}
