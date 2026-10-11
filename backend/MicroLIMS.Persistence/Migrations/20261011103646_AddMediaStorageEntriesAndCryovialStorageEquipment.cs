using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaStorageEntriesAndCryovialStorageEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StorageEquipmentId",
                table: "Cryovials",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaStorageEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaId = table.Column<int>(type: "integer", nullable: false),
                    Form = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: true),
                    LocationText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaStorageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaStorageEntries_EquipmentInventories_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "EquipmentInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaStorageEntries_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cryovials_StorageEquipmentId",
                table: "Cryovials",
                column: "StorageEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaStorageEntries_EquipmentId",
                table: "MediaStorageEntries",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaStorageEntries_MediaId_Form_EquipmentId_LocationText",
                table: "MediaStorageEntries",
                columns: new[] { "MediaId", "Form", "EquipmentId", "LocationText" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cryovials_EquipmentInventories_StorageEquipmentId",
                table: "Cryovials",
                column: "StorageEquipmentId",
                principalTable: "EquipmentInventories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cryovials_EquipmentInventories_StorageEquipmentId",
                table: "Cryovials");

            migrationBuilder.DropTable(
                name: "MediaStorageEntries");

            migrationBuilder.DropIndex(
                name: "IX_Cryovials_StorageEquipmentId",
                table: "Cryovials");

            migrationBuilder.DropColumn(
                name: "StorageEquipmentId",
                table: "Cryovials");
        }
    }
}
