using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaIncubationConditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- a. CreateTable MediaIncubationConditions with FK to MediaProducts (Restrict), then unique 5-column index ----
            migrationBuilder.CreateTable(
                name: "MediaIncubationConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaProductId = table.Column<int>(type: "integer", nullable: false),
                    IncubationMinHours = table.Column<int>(type: "integer", nullable: false),
                    IncubationMaxHours = table.Column<int>(type: "integer", nullable: false),
                    TemperatureMin = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    TemperatureMax = table.Column<decimal>(type: "numeric(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaIncubationConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaIncubationConditions_MediaProducts_MediaProductId",
                        column: x => x.MediaProductId,
                        principalTable: "MediaProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaIncubationConditions_MediaProductId_IncubationMinHours~",
                table: "MediaIncubationConditions",
                columns: new[] { "MediaProductId", "IncubationMinHours", "IncubationMaxHours", "TemperatureMin", "TemperatureMax" },
                unique: true);

            // ---- b. AddColumn MediaIncubationConditionId int NULL on MediaConfigurations and TestWorkflowStepMedias ----
            migrationBuilder.AddColumn<int>(
                name: "MediaIncubationConditionId",
                table: "MediaConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaIncubationConditionId",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: true);

            // ---- c. Sql backfill in ONE DO $$ block ----
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    v_multi_config_list text;
                    v_unlinked_step_media_count integer;
                BEGIN
                    -- (1) If any product has more than one MediaConfigurations row, RAISE EXCEPTION listing product names and counts, saying to merge them before migrating.
                    SELECT string_agg(p.""Name"" || ' (' || cnt || ' rows)', ', ')
                    INTO v_multi_config_list
                    FROM (
                        SELECT ""MediaProductId"", count(*) AS cnt
                        FROM ""MediaConfigurations""
                        GROUP BY ""MediaProductId""
                        HAVING count(*) > 1
                    ) dup
                    JOIN ""MediaProducts"" p ON p.""Id"" = dup.""MediaProductId"";

                    IF v_multi_config_list IS NOT NULL THEN
                        RAISE EXCEPTION 'Cannot migrate: the following media product(s) have more than one MediaConfiguration row and must be merged before migrating: %', v_multi_config_list;
                    END IF;

                    -- (2) INSERT one condition per DISTINCT (MediaProductId, IncubationMinHours, IncubationMaxHours, TemperatureMin, TemperatureMax) from MediaConfigurations.
                    INSERT INTO ""MediaIncubationConditions"" (""MediaProductId"", ""IncubationMinHours"", ""IncubationMaxHours"", ""TemperatureMin"", ""TemperatureMax"")
                    SELECT DISTINCT ""MediaProductId"", ""IncubationMinHours"", ""IncubationMaxHours"", ""TemperatureMin"", ""TemperatureMax""
                    FROM ""MediaConfigurations"";

                    -- (3) UPDATE MediaConfigurations with their matching condition id.
                    UPDATE ""MediaConfigurations"" c
                    SET ""MediaIncubationConditionId"" = mic.""Id""
                    FROM ""MediaIncubationConditions"" mic
                    WHERE mic.""MediaProductId"" = c.""MediaProductId""
                      AND mic.""IncubationMinHours"" = c.""IncubationMinHours""
                      AND mic.""IncubationMaxHours"" = c.""IncubationMaxHours""
                      AND mic.""TemperatureMin"" = c.""TemperatureMin""
                      AND mic.""TemperatureMax"" = c.""TemperatureMax"";

                    -- (4) UPDATE TestWorkflowStepMedias with the condition of their MediaConfigurationId's configuration.
                    UPDATE ""TestWorkflowStepMedias"" sm
                    SET ""MediaIncubationConditionId"" = c.""MediaIncubationConditionId""
                    FROM ""MediaConfigurations"" c
                    WHERE sm.""MediaConfigurationId"" IS NOT NULL
                      AND sm.""MediaConfigurationId"" = c.""Id"";

                    -- (5) For step media still without a condition whose Materials row has a MediaProductId, and whose snapshot is valid (IncubationMinHours > 0, IncubationMaxHours >= IncubationMinHours, TempMin <= TempMax):
                    --     - INSERT the missing (material product, snapshot) conditions using WHERE NOT EXISTS;
                    --     - then link those rows.
                    INSERT INTO ""MediaIncubationConditions"" (""MediaProductId"", ""IncubationMinHours"", ""IncubationMaxHours"", ""TemperatureMin"", ""TemperatureMax"")
                    SELECT DISTINCT m.""MediaProductId"", sm.""IncubationMinHours"", sm.""IncubationMaxHours"", sm.""TempMin"", sm.""TempMax""
                    FROM ""TestWorkflowStepMedias"" sm
                    JOIN ""Materials"" m ON m.""Id"" = sm.""MaterialId""
                    WHERE sm.""MediaIncubationConditionId"" IS NULL
                      AND m.""MediaProductId"" IS NOT NULL
                      AND sm.""IncubationMinHours"" > 0
                      AND sm.""IncubationMaxHours"" >= sm.""IncubationMinHours""
                      AND sm.""TempMin"" <= sm.""TempMax""
                      AND NOT EXISTS (
                          SELECT 1 FROM ""MediaIncubationConditions"" mic
                          WHERE mic.""MediaProductId"" = m.""MediaProductId""
                            AND mic.""IncubationMinHours"" = sm.""IncubationMinHours""
                            AND mic.""IncubationMaxHours"" = sm.""IncubationMaxHours""
                            AND mic.""TemperatureMin"" = sm.""TempMin""
                            AND mic.""TemperatureMax"" = sm.""TempMax""
                      );

                    UPDATE ""TestWorkflowStepMedias"" sm
                    SET ""MediaIncubationConditionId"" = mic.""Id""
                    FROM ""Materials"" m
                    JOIN ""MediaIncubationConditions"" mic
                      ON mic.""MediaProductId"" = m.""MediaProductId""
                    WHERE sm.""MaterialId"" = m.""Id""
                      AND sm.""MediaIncubationConditionId"" IS NULL
                      AND m.""MediaProductId"" IS NOT NULL
                      AND sm.""IncubationMinHours"" > 0
                      AND sm.""IncubationMaxHours"" >= sm.""IncubationMinHours""
                      AND sm.""TempMin"" <= sm.""TempMax""
                      AND mic.""IncubationMinHours"" = sm.""IncubationMinHours""
                      AND mic.""IncubationMaxHours"" = sm.""IncubationMaxHours""
                      AND mic.""TemperatureMin"" = sm.""TempMin""
                      AND mic.""TemperatureMax"" = sm.""TempMax"";

                    -- (6) RAISE NOTICE how many step media remain without a condition; this is not an error.
                    SELECT count(*)
                    INTO v_unlinked_step_media_count
                    FROM ""TestWorkflowStepMedias""
                    WHERE ""MediaIncubationConditionId"" IS NULL;

                    RAISE NOTICE 'Step media remaining without an incubation condition: %', v_unlinked_step_media_count;

                    -- (7) RAISE EXCEPTION if any MediaConfigurations row still has a NULL condition.
                    IF EXISTS (SELECT 1 FROM ""MediaConfigurations"" WHERE ""MediaIncubationConditionId"" IS NULL) THEN
                        RAISE EXCEPTION 'MediaConfigurations has rows with unresolved MediaIncubationConditionId after backfill';
                    END IF;
                END $$;
            ");

            // ---- d. AlterColumn MediaConfigurations.MediaIncubationConditionId to NOT NULL ----
            migrationBuilder.AlterColumn<int>(
                name: "MediaIncubationConditionId",
                table: "MediaConfigurations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // ---- e. Drop the old objects ----
            migrationBuilder.DropForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaConfigurations_MediaConfigurati~",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowStepMedias_MediaConfigurationId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropColumn(
                name: "MediaConfigurationId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropIndex(
                name: "IX_MediaConfigurations_MediaProductId_IncubationMinHours_Incub~",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "IncubationMinHours",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "IncubationMaxHours",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "TemperatureMin",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "TemperatureMax",
                table: "MediaConfigurations");

            // ---- f. Create the new indexes and FKs ----
            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_MediaProductId",
                table: "MediaConfigurations",
                column: "MediaProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_MediaIncubationConditionId",
                table: "MediaConfigurations",
                column: "MediaIncubationConditionId");

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_MediaIncubationConditionId",
                table: "TestWorkflowStepMedias",
                column: "MediaIncubationConditionId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaConfigurations_MediaIncubationConditions_MediaIncubati~",
                table: "MediaConfigurations",
                column: "MediaIncubationConditionId",
                principalTable: "MediaIncubationConditions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaIncubationConditions_MediaIncub~",
                table: "TestWorkflowStepMedias",
                column: "MediaIncubationConditionId",
                principalTable: "MediaIncubationConditions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_MediaConfigurations_MediaIncubationConditions_MediaIncubati~",
                table: "MediaConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaIncubationConditions_MediaIncub~",
                table: "TestWorkflowStepMedias");

            // Re-add the four MediaConfigurations columns (nullable first)
            migrationBuilder.AddColumn<int>(
                name: "IncubationMinHours",
                table: "MediaConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncubationMaxHours",
                table: "MediaConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureMin",
                table: "MediaConfigurations",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureMax",
                table: "MediaConfigurations",
                type: "numeric",
                nullable: true);

            // Re-add TestWorkflowStepMedias.MediaConfigurationId (nullable)
            migrationBuilder.AddColumn<int>(
                name: "MediaConfigurationId",
                table: "TestWorkflowStepMedias",
                type: "integer",
                nullable: true);

            // Backfill in SQL
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Fill MediaConfigurations columns from condition
                    UPDATE ""MediaConfigurations"" c
                    SET ""IncubationMinHours"" = mic.""IncubationMinHours"",
                        ""IncubationMaxHours"" = mic.""IncubationMaxHours"",
                        ""TemperatureMin"" = mic.""TemperatureMin"",
                        ""TemperatureMax"" = mic.""TemperatureMax""
                    FROM ""MediaIncubationConditions"" mic
                    WHERE c.""MediaIncubationConditionId"" = mic.""Id"";

                    -- Fill TestWorkflowStepMedias.MediaConfigurationId with the configuration of the same product whose condition equals the step medium's condition, else NULL
                    UPDATE ""TestWorkflowStepMedias"" sm
                    SET ""MediaConfigurationId"" = c.""Id""
                    FROM ""MediaConfigurations"" c
                    WHERE sm.""MediaIncubationConditionId"" IS NOT NULL
                      AND c.""MediaIncubationConditionId"" = sm.""MediaIncubationConditionId"";
                END $$;
            ");

            // Alter the four MediaConfigurations columns to NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "IncubationMinHours",
                table: "MediaConfigurations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IncubationMaxHours",
                table: "MediaConfigurations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TemperatureMin",
                table: "MediaConfigurations",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TemperatureMax",
                table: "MediaConfigurations",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            // Restore the old unique index and FK
            migrationBuilder.CreateIndex(
                name: "IX_MediaConfigurations_MediaProductId_IncubationMinHours_Incub~",
                table: "MediaConfigurations",
                columns: new[] { "MediaProductId", "IncubationMinHours", "IncubationMaxHours", "TemperatureMin", "TemperatureMax" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestWorkflowStepMedias_MediaConfigurationId",
                table: "TestWorkflowStepMedias",
                column: "MediaConfigurationId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestWorkflowStepMedias_MediaConfigurations_MediaConfigurati~",
                table: "TestWorkflowStepMedias",
                column: "MediaConfigurationId",
                principalTable: "MediaConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Drop the new indexes, FKs, columns, and table
            migrationBuilder.DropIndex(
                name: "IX_MediaConfigurations_MediaProductId",
                table: "MediaConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_MediaConfigurations_MediaIncubationConditionId",
                table: "MediaConfigurations");

            migrationBuilder.DropColumn(
                name: "MediaIncubationConditionId",
                table: "MediaConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_TestWorkflowStepMedias_MediaIncubationConditionId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropColumn(
                name: "MediaIncubationConditionId",
                table: "TestWorkflowStepMedias");

            migrationBuilder.DropTable(
                name: "MediaIncubationConditions");
        }
    }
}
