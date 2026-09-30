using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTitrantStandardization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TitrantStandardizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SolutionPreparationId = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    SettingsSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    MeanFactor = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    RsdPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StandardizedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StandardizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitrantStandardizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TitrantStandardizations_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TitrantStandardizations_SolutionPreparations_SolutionPrepar~",
                        column: x => x.SolutionPreparationId,
                        principalTable: "SolutionPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TitrantStandardizationReplicates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TitrantStandardizationId = table.Column<int>(type: "integer", nullable: false),
                    ReplicateNo = table.Column<int>(type: "integer", nullable: false),
                    StandardMaterialId = table.Column<int>(type: "integer", nullable: true),
                    StandardWeightMg = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    StandardPurityPercent = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ReferencePreparationId = table.Column<int>(type: "integer", nullable: true),
                    ReferenceVolumeMl = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    ReferenceFactor = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    TitrantVolumeMl = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    BlankMl = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: true),
                    Factor = table.Column<decimal>(type: "numeric(28,10)", precision: 28, scale: 10, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitrantStandardizationReplicates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TitrantStandardizationReplicates_Materials_StandardMaterial~",
                        column: x => x.StandardMaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TitrantStandardizationReplicates_SolutionPreparations_Refer~",
                        column: x => x.ReferencePreparationId,
                        principalTable: "SolutionPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TitrantStandardizationReplicates_TitrantStandardizations_Ti~",
                        column: x => x.TitrantStandardizationId,
                        principalTable: "TitrantStandardizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TitrantStandardizationReplicates_ReferencePreparationId",
                table: "TitrantStandardizationReplicates",
                column: "ReferencePreparationId");

            migrationBuilder.CreateIndex(
                name: "IX_TitrantStandardizationReplicates_StandardMaterialId",
                table: "TitrantStandardizationReplicates",
                column: "StandardMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_TitrantStandardizationReplicates_TitrantStandardizationId",
                table: "TitrantStandardizationReplicates",
                column: "TitrantStandardizationId");

            migrationBuilder.CreateIndex(
                name: "IX_TitrantStandardizations_SignatureId",
                table: "TitrantStandardizations",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_TitrantStandardizations_SolutionPreparationId_StandardizedAt",
                table: "TitrantStandardizations",
                columns: new[] { "SolutionPreparationId", "StandardizedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TitrantStandardizationReplicates");

            migrationBuilder.DropTable(
                name: "TitrantStandardizations");
        }
    }
}
