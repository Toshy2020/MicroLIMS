using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixConfirmatoryPlateWorkflowStepNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONF-214: 20260810225125 created the column NOT NULL with an unfiltered unique index,
            // while the model (int?) and configuration expect a nullable column and a filtered index.
            migrationBuilder.DropIndex(
                name: "IX_ConfirmatoryPlateObservations_WorkflowStepResultId_Material~",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.AlterColumn<int>(
                name: "WorkflowStepResultId",
                table: "ConfirmatoryPlateObservations",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryPlateObservations_WorkflowStepResultId_Material~",
                table: "ConfirmatoryPlateObservations",
                columns: new[] { "WorkflowStepResultId", "MaterialId" },
                unique: true,
                filter: "\"WorkflowStepResultId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Regulated rows are never deleted: refuse the rollback while location-linked plates exist.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "ConfirmatoryPlateObservations" WHERE "WorkflowStepResultId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert FixConfirmatoryPlateWorkflowStepNullable: ConfirmatoryPlateObservations contains rows with a NULL WorkflowStepResultId (location-linked plates). These regulated records must not be deleted; resolve them manually first.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_ConfirmatoryPlateObservations_WorkflowStepResultId_Material~",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.AlterColumn<int>(
                name: "WorkflowStepResultId",
                table: "ConfirmatoryPlateObservations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryPlateObservations_WorkflowStepResultId_Material~",
                table: "ConfirmatoryPlateObservations",
                columns: new[] { "WorkflowStepResultId", "MaterialId" },
                unique: true);
        }
    }
}
