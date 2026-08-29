using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyMediaTablesAndTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaChallengeSpecs");

            migrationBuilder.DropTable(
                name: "TestDefinitionMedias");

            migrationBuilder.DropTable(
                name: "MediaTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaChallengeSpecs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganismId = table.Column<int>(type: "integer", nullable: false),
                    ChallengeRole = table.Column<int>(type: "integer", nullable: true),
                    EvaluationType = table.Column<int>(type: "integer", nullable: false),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: true),
                    MaterialName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaChallengeSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaChallengeSpecs_Organisms_OrganismId",
                        column: x => x.OrganismId,
                        principalTable: "Organisms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovedTestCodes = table.Column<List<string>>(type: "text[]", nullable: false),
                    Class = table.Column<int>(type: "integer", nullable: false),
                    IncubationMaxHours = table.Column<int>(type: "integer", nullable: false),
                    IncubationMinHours = table.Column<int>(type: "integer", nullable: false),
                    RecoveryPercentMax = table.Column<decimal>(type: "numeric", nullable: true),
                    RecoveryPercentMin = table.Column<decimal>(type: "numeric", nullable: true),
                    RequiredTemperatureMax = table.Column<decimal>(type: "numeric", nullable: false),
                    RequiredTemperatureMin = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestDefinitionMedias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: false),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDefinitionMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestDefinitionMedias_MediaTypes_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalTable: "MediaTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDefinitionMedias_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaChallengeSpecs_MaterialName_EvaluationType_OrganismId_~",
                table: "MediaChallengeSpecs",
                columns: new[] { "MaterialName", "EvaluationType", "OrganismId", "ChallengeRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaChallengeSpecs_OrganismId",
                table: "MediaChallengeSpecs",
                column: "OrganismId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaTypes_Class",
                table: "MediaTypes",
                column: "Class",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitionMedias_MediaTypeId",
                table: "TestDefinitionMedias",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitionMedias_TestDefinitionId_MediaTypeId_StepName",
                table: "TestDefinitionMedias",
                columns: new[] { "TestDefinitionId", "MediaTypeId", "StepName" },
                unique: true);
        }
    }
}
