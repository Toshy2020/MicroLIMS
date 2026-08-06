using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceGptWithMediaEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incubations_TestOrders_TestOrderId",
                table: "Incubations");

            migrationBuilder.DropTable(
                name: "ExpectedIndicationResults");

            migrationBuilder.DropTable(
                name: "GptChallengeResults");

            migrationBuilder.AddColumn<bool>(
                name: "IsReleasedForUse",
                table: "Media",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Data-preserving: carry forward the old computed
            // IsReleasedForUse (GptStage == Release(3) && Status ==
            // Active(1)) into the new persisted column before GptStage
            // is dropped below, so lots already released under the GPT
            // module don't silently revert to unreleased.
            migrationBuilder.Sql(
                "UPDATE \"Media\" SET \"IsReleasedForUse\" = true WHERE \"GptStage\" = 3 AND \"Status\" = 1;");

            migrationBuilder.DropColumn(
                name: "GptStage",
                table: "Media");

            migrationBuilder.AlterColumn<int>(
                name: "TestOrderId",
                table: "Incubations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "MediaChallengeSpecs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialName = table.Column<string>(type: "text", nullable: false),
                    EvaluationType = table.Column<int>(type: "integer", nullable: false),
                    OrganismName = table.Column<string>(type: "text", nullable: false),
                    ChallengeRole = table.Column<int>(type: "integer", nullable: true),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaChallengeSpecs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaId = table.Column<int>(type: "integer", nullable: false),
                    EvaluationType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaEvaluations_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MediaEvaluationChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaEvaluationId = table.Column<int>(type: "integer", nullable: false),
                    OrganismName = table.Column<string>(type: "text", nullable: false),
                    CryovialId = table.Column<int>(type: "integer", nullable: true),
                    ChallengeRole = table.Column<int>(type: "integer", nullable: true),
                    InitialInoculum = table.Column<string>(type: "text", nullable: false),
                    IncubationId = table.Column<int>(type: "integer", nullable: true),
                    OldMediaCount = table.Column<decimal>(type: "numeric", nullable: true),
                    NewMediaCount = table.Column<decimal>(type: "numeric", nullable: true),
                    RecoveryPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    GrowthObserved = table.Column<bool>(type: "boolean", nullable: true),
                    ObservedDescription = table.Column<string>(type: "text", nullable: true),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: true),
                    IsTurbid = table.Column<bool>(type: "boolean", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaEvaluationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaEvaluationChallenges_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaEvaluationChallenges_Incubations_IncubationId",
                        column: x => x.IncubationId,
                        principalTable: "Incubations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaEvaluationChallenges_MediaEvaluations_MediaEvaluationId",
                        column: x => x.MediaEvaluationId,
                        principalTable: "MediaEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaChallengeSpecs_MaterialName_EvaluationType_OrganismNam~",
                table: "MediaChallengeSpecs",
                columns: new[] { "MaterialName", "EvaluationType", "OrganismName", "ChallengeRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluationChallenges_CryovialId",
                table: "MediaEvaluationChallenges",
                column: "CryovialId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluationChallenges_IncubationId",
                table: "MediaEvaluationChallenges",
                column: "IncubationId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluationChallenges_MediaEvaluationId",
                table: "MediaEvaluationChallenges",
                column: "MediaEvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluations_MediaId",
                table: "MediaEvaluations",
                column: "MediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incubations_TestOrders_TestOrderId",
                table: "Incubations",
                column: "TestOrderId",
                principalTable: "TestOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incubations_TestOrders_TestOrderId",
                table: "Incubations");

            migrationBuilder.DropTable(
                name: "MediaChallengeSpecs");

            migrationBuilder.DropTable(
                name: "MediaEvaluationChallenges");

            migrationBuilder.DropTable(
                name: "MediaEvaluations");

            migrationBuilder.DropColumn(
                name: "IsReleasedForUse",
                table: "Media");

            migrationBuilder.AddColumn<int>(
                name: "GptStage",
                table: "Media",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "TestOrderId",
                table: "Incubations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ExpectedIndicationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: false),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: false),
                    MaterialName = table.Column<string>(type: "text", nullable: false),
                    OrganismName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpectedIndicationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpectedIndicationResults_MediaTypes_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalTable: "MediaTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GptChallengeResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CryovialId = table.Column<int>(type: "integer", nullable: true),
                    MediaId = table.Column<int>(type: "integer", nullable: false),
                    Atcc = table.Column<string>(type: "text", nullable: true),
                    ExpectedDescription = table.Column<string>(type: "text", nullable: true),
                    InitialInoculum = table.Column<string>(type: "text", nullable: true),
                    NegativeControlGrowth = table.Column<bool>(type: "boolean", nullable: false),
                    NewMediaResult = table.Column<int>(type: "integer", nullable: true),
                    ObservationText = table.Column<string>(type: "text", nullable: true),
                    OldMediaResult = table.Column<int>(type: "integer", nullable: true),
                    OrganismName = table.Column<string>(type: "text", nullable: false),
                    Panel = table.Column<string>(type: "text", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecoveryPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    TurbidResult = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GptChallengeResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GptChallengeResults_Cryovials_CryovialId",
                        column: x => x.CryovialId,
                        principalTable: "Cryovials",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GptChallengeResults_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpectedIndicationResults_MediaTypeId",
                table: "ExpectedIndicationResults",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GptChallengeResults_CryovialId",
                table: "GptChallengeResults",
                column: "CryovialId");

            migrationBuilder.CreateIndex(
                name: "IX_GptChallengeResults_MediaId",
                table: "GptChallengeResults",
                column: "MediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incubations_TestOrders_TestOrderId",
                table: "Incubations",
                column: "TestOrderId",
                principalTable: "TestOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
