using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolutionMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolutionMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ShelfLifeValue = table.Column<int>(type: "integer", nullable: false),
                    ShelfLifeUnit = table.Column<int>(type: "integer", nullable: false),
                    StorageCondition = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FinalVolumeMl = table.Column<decimal>(type: "numeric(10,3)", nullable: false),
                    PhTarget = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    PhTolerance = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    PhAdjustingEntryId = table.Column<int>(type: "integer", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NominalStrength = table.Column<decimal>(type: "numeric(10,5)", nullable: true),
                    StrengthUnit = table.Column<int>(type: "integer", nullable: true),
                    StandardizationMode = table.Column<int>(type: "integer", nullable: true),
                    StandardEntryId = table.Column<int>(type: "integer", nullable: true),
                    EquivalenceMgPerMl = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    ReferenceSolutionId = table.Column<int>(type: "integer", nullable: true),
                    BlankRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ReplicateCount = table.Column<int>(type: "integer", nullable: true),
                    FactorMin = table.Column<decimal>(type: "numeric(8,5)", nullable: true),
                    FactorMax = table.Column<decimal>(type: "numeric(8,5)", nullable: true),
                    MaxRsdPercent = table.Column<decimal>(type: "numeric(6,3)", nullable: true),
                    ValidityDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionMasters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolutionMasters_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionMasters_MaterialMasterEntries_PhAdjustingEntryId",
                        column: x => x.PhAdjustingEntryId,
                        principalTable: "MaterialMasterEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionMasters_MaterialMasterEntries_StandardEntryId",
                        column: x => x.StandardEntryId,
                        principalTable: "MaterialMasterEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionMasters_SolutionMasters_ReferenceSolutionId",
                        column: x => x.ReferenceSolutionId,
                        principalTable: "SolutionMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SolutionComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolutionMasterId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    MaterialMasterEntryId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolutionComponents_MaterialMasterEntries_MaterialMasterEntr~",
                        column: x => x.MaterialMasterEntryId,
                        principalTable: "MaterialMasterEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolutionComponents_SolutionMasters_SolutionMasterId",
                        column: x => x.SolutionMasterId,
                        principalTable: "SolutionMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolutionComponents_MaterialMasterEntryId",
                table: "SolutionComponents",
                column: "MaterialMasterEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionComponents_SolutionMasterId_Order",
                table: "SolutionComponents",
                columns: new[] { "SolutionMasterId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SolutionMasters_PhAdjustingEntryId",
                table: "SolutionMasters",
                column: "PhAdjustingEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionMasters_ReferenceSolutionId",
                table: "SolutionMasters",
                column: "ReferenceSolutionId");

            migrationBuilder.CreateIndex(
                name: "IX_SolutionMasters_SectionId_Name",
                table: "SolutionMasters",
                columns: new[] { "SectionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolutionMasters_StandardEntryId",
                table: "SolutionMasters",
                column: "StandardEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolutionComponents");

            migrationBuilder.DropTable(
                name: "SolutionMasters");
        }
    }
}
