using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBiologicalWasteDestruction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BiologicalWasteDestructions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    AutoclaveEquipmentId = table.Column<int>(type: "integer", nullable: false),
                    AutoclaveProgramId = table.Column<int>(type: "integer", nullable: false),
                    ProgramCodeSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProgramNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LoadTypeSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemperatureSnapshot = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CycleTimeMinutesSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CycleNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CycleStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiologicalWasteDestructions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiologicalWasteDestructions_AutoclavePrograms_AutoclaveProg~",
                        column: x => x.AutoclaveProgramId,
                        principalTable: "AutoclavePrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiologicalWasteDestructions_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiologicalWasteDestructions_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiologicalWasteDestructions_Equipment_AutoclaveEquipmentId",
                        column: x => x.AutoclaveEquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BiologicalWasteDestructionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BiologicalWasteDestructionId = table.Column<int>(type: "integer", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    ThawEventId = table.Column<int>(type: "integer", nullable: true),
                    CryovialId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiologicalWasteDestructionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BiologicalWasteDestructionItems_BiologicalWasteDestructions~",
                        column: x => x.BiologicalWasteDestructionId,
                        principalTable: "BiologicalWasteDestructions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiologicalWasteDestructionItems_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BiologicalWasteDestructionItems_ThawEvents_ThawEventId",
                        column: x => x.ThawEventId,
                        principalTable: "ThawEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiologicalWasteDestructionItems_BiologicalWasteDestructionId",
                table: "BiologicalWasteDestructionItems",
                column: "BiologicalWasteDestructionId");

            migrationBuilder.CreateIndex(
                name: "IX_BiologicalWasteDestructionItems_CryovialId",
                table: "BiologicalWasteDestructionItems",
                column: "CryovialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiologicalWasteDestructionItems_ThawEventId",
                table: "BiologicalWasteDestructionItems",
                column: "ThawEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiologicalWasteDestructions_AutoclaveEquipmentId",
                table: "BiologicalWasteDestructions",
                column: "AutoclaveEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BiologicalWasteDestructions_AutoclaveProgramId",
                table: "BiologicalWasteDestructions",
                column: "AutoclaveProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_BiologicalWasteDestructions_SectionId",
                table: "BiologicalWasteDestructions",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_BiologicalWasteDestructions_SignatureId",
                table: "BiologicalWasteDestructions",
                column: "SignatureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiologicalWasteDestructionItems");

            migrationBuilder.DropTable(
                name: "BiologicalWasteDestructions");
        }
    }
}
