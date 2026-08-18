using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleLocationRawReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawReadings",
                table: "SampleLocations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentDocumentAccessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentInventoryId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentDocumentAccessLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentInventoryId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_EquipmentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentDocuments_EquipmentDocuments_SupersededByDocumentId",
                        column: x => x.SupersededByDocumentId,
                        principalTable: "EquipmentDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentDocuments_EquipmentInventories_EquipmentInventoryId",
                        column: x => x.EquipmentInventoryId,
                        principalTable: "EquipmentInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentInventoryId = table.Column<int>(type: "integer", nullable: false),
                    PreviousStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentStatusHistories_EquipmentInventories_EquipmentInve~",
                        column: x => x.EquipmentInventoryId,
                        principalTable: "EquipmentInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentStatusHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentAccessLogs_DocumentId",
                table: "EquipmentDocumentAccessLogs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentAccessLogs_EquipmentInventoryId",
                table: "EquipmentDocumentAccessLogs",
                column: "EquipmentInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocumentAccessLogs_UserId",
                table: "EquipmentDocumentAccessLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocuments_EquipmentInventoryId",
                table: "EquipmentDocuments",
                column: "EquipmentInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocuments_EquipmentInventoryId_DocumentType_Status",
                table: "EquipmentDocuments",
                columns: new[] { "EquipmentInventoryId", "DocumentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocuments_EquipmentInventoryId_Status",
                table: "EquipmentDocuments",
                columns: new[] { "EquipmentInventoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocuments_SupersededByDocumentId",
                table: "EquipmentDocuments",
                column: "SupersededByDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentDocuments_UploadedByUserId",
                table: "EquipmentDocuments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStatusHistories_ChangedByUserId",
                table: "EquipmentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStatusHistories_EquipmentInventoryId",
                table: "EquipmentStatusHistories",
                column: "EquipmentInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentStatusHistories_EquipmentInventoryId_ChangedAt",
                table: "EquipmentStatusHistories",
                columns: new[] { "EquipmentInventoryId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentDocumentAccessLogs");

            migrationBuilder.DropTable(
                name: "EquipmentDocuments");

            migrationBuilder.DropTable(
                name: "EquipmentStatusHistories");

            migrationBuilder.DropColumn(
                name: "RawReadings",
                table: "SampleLocations");
        }
    }
}
