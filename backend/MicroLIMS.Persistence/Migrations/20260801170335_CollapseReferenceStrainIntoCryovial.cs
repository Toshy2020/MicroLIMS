using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollapseReferenceStrainIntoCryovial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cryovials_ReferenceStrains_ReferenceStrainId",
                table: "Cryovials");

            migrationBuilder.DropForeignKey(
                name: "FK_IdentityConfirmationEntry_Cryovials_CryovialId",
                table: "IdentityConfirmationEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_IdentityConfirmationEntry_Equipment_IncubatorEquipmentId",
                table: "IdentityConfirmationEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_IdentityConfirmationEntry_Media_MediaId",
                table: "IdentityConfirmationEntry");

            migrationBuilder.DropForeignKey(
                name: "FK_IdentityConfirmationEntry_ReferenceStrains_ReferenceStrainId",
                table: "IdentityConfirmationEntry");

            migrationBuilder.DropTable(
                name: "PassageEvents");

            migrationBuilder.DropTable(
                name: "ReferenceStrains");

            migrationBuilder.DropIndex(
                name: "IX_Cryovials_ReferenceStrainId",
                table: "Cryovials");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IdentityConfirmationEntry",
                table: "IdentityConfirmationEntry");

            migrationBuilder.DropIndex(
                name: "IX_IdentityConfirmationEntry_ReferenceStrainId",
                table: "IdentityConfirmationEntry");

            migrationBuilder.DropColumn(
                name: "ThawedAt",
                table: "Cryovials");

            migrationBuilder.DropColumn(
                name: "ReferenceStrainId",
                table: "IdentityConfirmationEntry");

            migrationBuilder.RenameTable(
                name: "IdentityConfirmationEntry",
                newName: "IdentityConfirmationEntries");

            migrationBuilder.RenameColumn(
                name: "ReferenceStrainId",
                table: "Cryovials",
                newName: "VialsRemaining");

            migrationBuilder.RenameColumn(
                name: "PassageNumber",
                table: "Cryovials",
                newName: "MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityConfirmationEntry_MediaId",
                table: "IdentityConfirmationEntries",
                newName: "IX_IdentityConfirmationEntries_MediaId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityConfirmationEntry_IncubatorEquipmentId",
                table: "IdentityConfirmationEntries",
                newName: "IX_IdentityConfirmationEntries_IncubatorEquipmentId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityConfirmationEntry_CryovialId",
                table: "IdentityConfirmationEntries",
                newName: "IX_IdentityConfirmationEntries_CryovialId");

            migrationBuilder.AddColumn<string>(
                name: "AtccNumber",
                table: "Materials",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtccNumber",
                table: "Cryovials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganismName",
                table: "Cryovials",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PreparedAt",
                table: "Cryovials",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<int>(
                name: "CryovialId",
                table: "IdentityConfirmationEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_IdentityConfirmationEntries",
                table: "IdentityConfirmationEntries",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ThawEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CryovialId = table.Column<int>(type: "integer", nullable: false),
                    ThawedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ThawedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThawEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThawEvents_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cryovials_MaterialId",
                table: "Cryovials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ThawEvents_CryovialId",
                table: "ThawEvents",
                column: "CryovialId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cryovials_Materials_MaterialId",
                table: "Cryovials",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityConfirmationEntries_Cryovials_CryovialId",
                table: "IdentityConfirmationEntries",
                column: "CryovialId",
                principalTable: "Cryovials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityConfirmationEntries_Equipment_IncubatorEquipmentId",
                table: "IdentityConfirmationEntries",
                column: "IncubatorEquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityConfirmationEntries_Media_MediaId",
                table: "IdentityConfirmationEntries",
                column: "MediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cryovials_Materials_MaterialId",
                table: "Cryovials");

            migrationBuilder.DropForeignKey(
                name: "FK_IdentityConfirmationEntries_Cryovials_CryovialId",
                table: "IdentityConfirmationEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_IdentityConfirmationEntries_Equipment_IncubatorEquipmentId",
                table: "IdentityConfirmationEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_IdentityConfirmationEntries_Media_MediaId",
                table: "IdentityConfirmationEntries");

            migrationBuilder.DropTable(
                name: "ThawEvents");

            migrationBuilder.DropIndex(
                name: "IX_Cryovials_MaterialId",
                table: "Cryovials");

            migrationBuilder.DropPrimaryKey(
                name: "PK_IdentityConfirmationEntries",
                table: "IdentityConfirmationEntries");

            migrationBuilder.DropColumn(
                name: "AtccNumber",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "AtccNumber",
                table: "Cryovials");

            migrationBuilder.DropColumn(
                name: "OrganismName",
                table: "Cryovials");

            migrationBuilder.DropColumn(
                name: "PreparedAt",
                table: "Cryovials");

            migrationBuilder.RenameTable(
                name: "IdentityConfirmationEntries",
                newName: "IdentityConfirmationEntry");

            migrationBuilder.RenameColumn(
                name: "VialsRemaining",
                table: "Cryovials",
                newName: "ReferenceStrainId");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "Cryovials",
                newName: "PassageNumber");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityConfirmationEntries_MediaId",
                table: "IdentityConfirmationEntry",
                newName: "IX_IdentityConfirmationEntry_MediaId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityConfirmationEntries_IncubatorEquipmentId",
                table: "IdentityConfirmationEntry",
                newName: "IX_IdentityConfirmationEntry_IncubatorEquipmentId");

            migrationBuilder.RenameIndex(
                name: "IX_IdentityConfirmationEntries_CryovialId",
                table: "IdentityConfirmationEntry",
                newName: "IX_IdentityConfirmationEntry_CryovialId");

            migrationBuilder.AddColumn<DateTime>(
                name: "ThawedAt",
                table: "Cryovials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CryovialId",
                table: "IdentityConfirmationEntry",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ReferenceStrainId",
                table: "IdentityConfirmationEntry",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_IdentityConfirmationEntry",
                table: "IdentityConfirmationEntry",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "PassageEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CryovialId = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PassageNumber = table.Column<int>(type: "integer", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassageEvents_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceStrains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    AtccNumber = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    DiscsRemaining = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ManufacturerName = table.Column<string>(type: "text", nullable: false),
                    NumberOfDiscs = table.Column<int>(type: "integer", nullable: false),
                    OrganismName = table.Column<string>(type: "text", nullable: false),
                    PassageNumber = table.Column<int>(type: "integer", nullable: false),
                    PhysicalCheckText = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StorageCondition = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceStrains", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cryovials_ReferenceStrainId",
                table: "Cryovials",
                column: "ReferenceStrainId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityConfirmationEntry_ReferenceStrainId",
                table: "IdentityConfirmationEntry",
                column: "ReferenceStrainId");

            migrationBuilder.CreateIndex(
                name: "IX_PassageEvents_CryovialId",
                table: "PassageEvents",
                column: "CryovialId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cryovials_ReferenceStrains_ReferenceStrainId",
                table: "Cryovials",
                column: "ReferenceStrainId",
                principalTable: "ReferenceStrains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityConfirmationEntry_Cryovials_CryovialId",
                table: "IdentityConfirmationEntry",
                column: "CryovialId",
                principalTable: "Cryovials",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityConfirmationEntry_Equipment_IncubatorEquipmentId",
                table: "IdentityConfirmationEntry",
                column: "IncubatorEquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityConfirmationEntry_Media_MediaId",
                table: "IdentityConfirmationEntry",
                column: "MediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_IdentityConfirmationEntry_ReferenceStrains_ReferenceStrainId",
                table: "IdentityConfirmationEntry",
                column: "ReferenceStrainId",
                principalTable: "ReferenceStrains",
                principalColumn: "Id");
        }
    }
}
