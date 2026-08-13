using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPathogenWorkflowRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- WorkflowType.DualPlate (ordinal 2) is removed; the enum now stops at Observation (1) ----
            // A DualPlate test's steps are re-typed by the StepResultType -> StepType
            // remap below, so the test itself becomes an ordinary Observation-workflow
            // test - Observation and DualPlate already shared the same step-driven engine.
            migrationBuilder.Sql(@"UPDATE ""TestDefinitions"" SET ""WorkflowType"" = 1 WHERE ""WorkflowType"" = 2;");

            // ---- TestWorkflowSteps: StepResultType (3 values) -> StepType (6 values) ----
            // Added alongside the old column, not renamed onto it: the old enum cannot be
            // permuted in place into the new one (values need to fan out, not just shift),
            // so both columns must exist together while rows are translated across.
            migrationBuilder.AddColumn<int>(
                name: "StepType",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // PlateCount (0) is unchanged in both enums - the AddColumn default above
            // already leaves every row at 0, so PlateCount rows need no statement here.

            // DualGrowth (2), the old two-plate final step, is the direct behavioural
            // ancestor of ConfirmatoryPlating (4) - both are the analyst-selected
            // confirmatory read at the end of a pathogen chain.
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepType"" = 4 WHERE ""StepResultType"" = 2;");

            // Growth (1) covered every non-count step with a single value - TSB, RVS,
            // and the single-plate final step (e.g. what preceded Salmonella's XLD_TSI)
            // were all "Growth". The new model splits that one value three ways, using
            // context the old enum itself did not carry:
            //   - IsFinalStep = true is the step whose result decided the workflow
            //     outcome -> SelectivePlating (3).
            //   - IsFinalStep = false and the step's MediaType is a selective broth
            //     (MediaClass.SelectiveBroth = 3, e.g. RVS) -> SelectiveBroth (2).
            //   - IsFinalStep = false otherwise (general enrichment broths, e.g. TSB)
            //     -> BrothEnrichment (1).
            migrationBuilder.Sql(@"
                UPDATE ""TestWorkflowSteps""
                SET ""StepType"" = 3
                WHERE ""StepResultType"" = 1 AND ""IsFinalStep"" = TRUE;");

            migrationBuilder.Sql(@"
                UPDATE ""TestWorkflowSteps"" s
                SET ""StepType"" = 2
                FROM ""MediaTypes"" mt
                WHERE s.""MediaTypeId"" = mt.""Id""
                  AND s.""StepResultType"" = 1
                  AND s.""IsFinalStep"" = FALSE
                  AND mt.""Class"" = 3;");

            migrationBuilder.Sql(@"
                UPDATE ""TestWorkflowSteps"" s
                SET ""StepType"" = 1
                FROM ""MediaTypes"" mt
                WHERE s.""MediaTypeId"" = mt.""Id""
                  AND s.""StepResultType"" = 1
                  AND s.""IsFinalStep"" = FALSE
                  AND mt.""Class"" <> 3;");

            migrationBuilder.DropColumn(
                name: "StepResultType",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "IsDualPlate",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "Plate1DefaultLabel",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "Plate2DefaultLabel",
                table: "TestWorkflowSteps");

            // ---- PathogenObservations: GrowthObserved (bool) -> Observation (GrowthObservation enum) ----
            migrationBuilder.AddColumn<int>(
                name: "Observation",
                table: "PathogenObservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // No data remap: the old boolean recorded only "something grew" and had
            // no way to express GrowthNonConforming - growth that does not match the
            // target organism's expected appearance. Mapping true -> GrowthConforming
            // would assert a morphology judgement the analyst was never shown and
            // never made, which is a fabricated record under ALCOA+. The table held
            // only pre-release development data (no signed or approved records, system
            // not yet in operational use) and has been emptied, so there is nothing to
            // migrate - the column is simply added empty.

            migrationBuilder.DropColumn(
                name: "GrowthObserved",
                table: "PathogenObservations");

            migrationBuilder.DropColumn(
                name: "PlateLabel",
                table: "PathogenObservations");

            // ---- Incubations: drop the dual-plate second-media column, add the incubation window ----
            migrationBuilder.DropForeignKey(
                name: "FK_Incubations_Media_Plate2MediaId",
                table: "Incubations");

            migrationBuilder.DropIndex(
                name: "IX_Incubations_Plate2MediaId",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "Plate2MediaId",
                table: "Incubations");

            migrationBuilder.AddColumn<DateTime>(
                name: "IncubationEndUtc",
                table: "Incubations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IncubationStartUtc",
                table: "Incubations",
                type: "timestamp with time zone",
                nullable: true);

            // ---- TestWorkflowSteps: target organism ----
            migrationBuilder.AddColumn<int>(
                name: "TargetOrganismId",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: true);

            // ---- New step-media and confirmatory-run tables ----
            migrationBuilder.CreateTable(
                name: "TestWorkflowStepMedias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestWorkflowStepId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    TempMin = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TempMax = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestWorkflowStepMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestWorkflowStepMedias_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestWorkflowStepMedias_TestWorkflowSteps_TestWorkflowStepId",
                        column: x => x.TestWorkflowStepId,
                        principalTable: "TestWorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IncubationId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StepType = table.Column<int>(type: "integer", nullable: false),
                    SelectivePlatingObservation = table.Column<int>(type: "integer", nullable: true),
                    ExpectedAppearanceSnapshot = table.Column<string>(type: "text", nullable: true),
                    ConfirmatoryResult = table.Column<int>(type: "integer", nullable: true),
                    BiochemicalResultText = table.Column<string>(type: "text", nullable: true),
                    BiochemicalAttachmentId = table.Column<int>(type: "integer", nullable: true),
                    SkippedBiochemical = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresBiochemical = table.Column<bool>(type: "boolean", nullable: false),
                    ReturnReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReturnedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedByUserId = table.Column<int>(type: "integer", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepResults_Incubations_IncubationId",
                        column: x => x.IncubationId,
                        principalTable: "Incubations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowStepResults_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfirmatoryMediaSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowStepResultId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    MediaId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    WasAnalystAdded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmatoryMediaSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfirmatoryMediaSelections_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmatoryMediaSelections_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmatoryMediaSelections_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmatoryMediaSelections_WorkflowStepResults_WorkflowSte~",
                        column: x => x.WorkflowStepResultId,
                        principalTable: "WorkflowStepResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConfirmatoryPlateObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WorkflowStepResultId = table.Column<int>(type: "integer", nullable: false),
                    MaterialId = table.Column<int>(type: "integer", nullable: false),
                    Observation = table.Column<int>(type: "integer", nullable: false),
                    ExpectedAppearanceSnapshot = table.Column<string>(type: "text", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmatoryPlateObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfirmatoryPlateObservations_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmatoryPlateObservations_WorkflowStepResults_WorkflowS~",
                        column: x => x.WorkflowStepResultId,
                        principalTable: "WorkflowStepResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowSteps_TargetOrganismId",
                table: "TestWorkflowSteps",
                column: "TargetOrganismId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryMediaSelections_EquipmentId",
                table: "ConfirmatoryMediaSelections",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryMediaSelections_MaterialId",
                table: "ConfirmatoryMediaSelections",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryMediaSelections_MediaId",
                table: "ConfirmatoryMediaSelections",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryMediaSelections_WorkflowStepResultId_MaterialId",
                table: "ConfirmatoryMediaSelections",
                columns: new[] { "WorkflowStepResultId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryPlateObservations_MaterialId",
                table: "ConfirmatoryPlateObservations",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryPlateObservations_WorkflowStepResultId_Material~",
                table: "ConfirmatoryPlateObservations",
                columns: new[] { "WorkflowStepResultId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_MaterialId",
                table: "TestWorkflowStepMedias",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_TestWorkflowStepId_MaterialId",
                table: "TestWorkflowStepMedias",
                columns: new[] { "TestWorkflowStepId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepResults_IncubationId",
                table: "WorkflowStepResults",
                column: "IncubationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepResults_TestOrderId",
                table: "WorkflowStepResults",
                column: "TestOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestWorkflowSteps_Organisms_TargetOrganismId",
                table: "TestWorkflowSteps",
                column: "TargetOrganismId",
                principalTable: "Organisms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestWorkflowSteps_Organisms_TargetOrganismId",
                table: "TestWorkflowSteps");

            migrationBuilder.DropTable(
                name: "ConfirmatoryMediaSelections");

            migrationBuilder.DropTable(
                name: "ConfirmatoryPlateObservations");

            migrationBuilder.DropTable(
                name: "TestWorkflowStepMedias");

            migrationBuilder.DropTable(
                name: "WorkflowStepResults");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowSteps_TargetOrganismId",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "TargetOrganismId",
                table: "TestWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "IncubationEndUtc",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "IncubationStartUtc",
                table: "Incubations");

            // ---- PathogenObservations: Observation -> GrowthObserved (lossy) ----
            migrationBuilder.AddColumn<bool>(
                name: "GrowthObserved",
                table: "PathogenObservations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // No data restore: Up() no longer converts historical data into Observation
            // (see the comment there), so there is nothing here to convert back either.
            // GrowthObserved is simply added at its default (false) for consistency with
            // that decision - not because rollback couldn't collapse Observation onto a
            // bool, but because this migration no longer asserts data across that
            // boundary in either direction.

            migrationBuilder.AddColumn<string>(
                name: "PlateLabel",
                table: "PathogenObservations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
            // No data to restore into PlateLabel - the free-text plate label was
            // discarded when this column was dropped in Up() and is not derivable
            // from anything the new model kept.

            migrationBuilder.DropColumn(
                name: "Observation",
                table: "PathogenObservations");

            // ---- Incubations: restore the dual-plate second-media column (empty) ----
            migrationBuilder.AddColumn<int>(
                name: "Plate2MediaId",
                table: "Incubations",
                type: "integer",
                nullable: true);
            // No data to restore - the post-refactor equivalent (ConfirmatoryMediaSelections,
            // already dropped above) is a one-to-many set of media per confirmatory run, not
            // a single second-plate column; there is no lossless way to fold it back down.

            migrationBuilder.CreateIndex(
                name: "IX_Incubations_Plate2MediaId",
                table: "Incubations",
                column: "Plate2MediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incubations_Media_Plate2MediaId",
                table: "Incubations",
                column: "Plate2MediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ---- TestWorkflowSteps: StepType -> StepResultType (lossy) ----
            migrationBuilder.AddColumn<int>(
                name: "StepResultType",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // PlateCount (0) is unchanged in both enums - rows stay at the AddColumn
            // default and need no statement.

            // ConfirmatoryPlating (4) is the direct descendant of DualGrowth (2).
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepResultType"" = 2 WHERE ""StepType"" = 4;");

            // BrothEnrichment (1), SelectiveBroth (2) and SelectivePlating (3) all
            // collapse back onto Growth (1) - the old enum had no concept of a broth
            // step distinct from a plate-reading step, only "growth yes/no". This is a
            // genuine information loss: a template edited after this migration ran,
            // using step types that did not exist before it, cannot be told apart from
            // pre-refactor Growth steps once rolled back.
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepResultType"" = 1 WHERE ""StepType"" IN (1, 2, 3);");

            // BiochemicalTest (5) has no pre-refactor equivalent at all - free-text
            // bench confirmation did not exist as a workflow step before this
            // migration. Mapped to Growth (1) purely so the column holds a valid
            // old-enum value; the biochemical result text and attachment are not
            // recoverable through StepResultType under any mapping.
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""StepResultType"" = 1 WHERE ""StepType"" = 5;");

            migrationBuilder.DropColumn(
                name: "StepType",
                table: "TestWorkflowSteps");

            migrationBuilder.AddColumn<bool>(
                name: "IsDualPlate",
                table: "TestWorkflowSteps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Not lossy: the old model kept IsDualPlate in sync with
            // StepResultType.DualGrowth by construction, so this is fully recoverable
            // from the StepResultType just restored above.
            migrationBuilder.Sql(@"UPDATE ""TestWorkflowSteps"" SET ""IsDualPlate"" = (""StepResultType"" = 2);");

            migrationBuilder.AddColumn<string>(
                name: "Plate1DefaultLabel",
                table: "TestWorkflowSteps",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Plate2DefaultLabel",
                table: "TestWorkflowSteps",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
            // No data to restore into either label - the analyst-editable default
            // plate labels were discarded when these columns were dropped in Up().

            // ---- WorkflowType.DualPlate is NOT restored ----
            // A test that was DualPlate before Up() is indistinguishable from a plain
            // Observation test afterwards - both read 1, and nothing else in the model
            // records which one it used to be. Guessing (e.g. from ConfirmatoryPlating
            // step presence) would produce a plausible-looking but unverifiable value
            // in a GMP record, which is worse than leaving it alone. WorkflowType is
            // therefore left as Observation on rollback.
        }
    }
}
