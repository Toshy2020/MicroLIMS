using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreparationMaterialTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiluentTypes");

            migrationBuilder.AddColumn<string>(
                name: "DiluentLotCode",
                table: "SamplePreparations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentMaterialId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentMediaId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentSolutionPreparationId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentSource",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NeutralizerLotCode",
                table: "SamplePreparations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerMaterialId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerMediaId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerSolutionPreparationId",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerSource",
                table: "SamplePreparations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentMaterialMasterEntryId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentMediaProductId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentSolutionMasterId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiluentSource",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerMaterialMasterEntryId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerMediaProductId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerSolutionMasterId",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NeutralizerSource",
                table: "ItemPreparationConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_DiluentMaterialId",
                table: "SamplePreparations",
                column: "DiluentMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_DiluentMediaId",
                table: "SamplePreparations",
                column: "DiluentMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_DiluentSolutionPreparationId",
                table: "SamplePreparations",
                column: "DiluentSolutionPreparationId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_NeutralizerMaterialId",
                table: "SamplePreparations",
                column: "NeutralizerMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_NeutralizerMediaId",
                table: "SamplePreparations",
                column: "NeutralizerMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_SamplePreparations_NeutralizerSolutionPreparationId",
                table: "SamplePreparations",
                column: "NeutralizerSolutionPreparationId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_DiluentMaterialMasterEntryId",
                table: "ItemPreparationConfigurations",
                column: "DiluentMaterialMasterEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_DiluentMediaProductId",
                table: "ItemPreparationConfigurations",
                column: "DiluentMediaProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_DiluentSolutionMasterId",
                table: "ItemPreparationConfigurations",
                column: "DiluentSolutionMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerMaterialMasterEntr~",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerMaterialMasterEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerMediaProductId",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerMediaProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerSolutionMasterId",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerSolutionMasterId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_MaterialMasterEntries_Diluent~",
                table: "ItemPreparationConfigurations",
                column: "DiluentMaterialMasterEntryId",
                principalTable: "MaterialMasterEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_MaterialMasterEntries_Neutral~",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerMaterialMasterEntryId",
                principalTable: "MaterialMasterEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_MediaProducts_DiluentMediaPro~",
                table: "ItemPreparationConfigurations",
                column: "DiluentMediaProductId",
                principalTable: "MediaProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_MediaProducts_NeutralizerMedi~",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerMediaProductId",
                principalTable: "MediaProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_SolutionMasters_DiluentSoluti~",
                table: "ItemPreparationConfigurations",
                column: "DiluentSolutionMasterId",
                principalTable: "SolutionMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemPreparationConfigurations_SolutionMasters_NeutralizerSo~",
                table: "ItemPreparationConfigurations",
                column: "NeutralizerSolutionMasterId",
                principalTable: "SolutionMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_Materials_DiluentMaterialId",
                table: "SamplePreparations",
                column: "DiluentMaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_Materials_NeutralizerMaterialId",
                table: "SamplePreparations",
                column: "NeutralizerMaterialId",
                principalTable: "Materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_Media_DiluentMediaId",
                table: "SamplePreparations",
                column: "DiluentMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_Media_NeutralizerMediaId",
                table: "SamplePreparations",
                column: "NeutralizerMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_SolutionPreparations_DiluentSolutionPrep~",
                table: "SamplePreparations",
                column: "DiluentSolutionPreparationId",
                principalTable: "SolutionPreparations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SamplePreparations_SolutionPreparations_NeutralizerSolution~",
                table: "SamplePreparations",
                column: "NeutralizerSolutionPreparationId",
                principalTable: "SolutionPreparations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill (plan 3.5, one audited run with a summary row, the same approach as the Master Testing Materials
            // backfill): each configuration's diluent / neutralizer text is matched, trimmed and case-insensitively, to a
            // master. Neutralizer text meaning "none" -> ContainedInMedium; otherwise the first of active media product
            // (name or code), active general solution recipe (name; any section) or active material entry (name or code)
            // that has a match decides, and only when exactly one candidate matches - an ambiguous name is not guessed.
            // The text is left as it is, approval status is not touched (nothing the Section Head signed changed), and
            // SamplePreparations are never touched. Unmatched values are listed in the summary row. Source ints:
            // 0 MediaProduct, 1 SolutionRecipe, 2 MaterialEntry, 3 ContainedInMedium; solution types 1 Diluent, 3 Reagent, 4 Neutralizer.
            migrationBuilder.Sql("""
            DO $backfill$
            DECLARE
                v_process constant text := 'Migration 20261105075244_PreparationMaterialTraceability';
                v_reason constant text := 'Diluent/neutralizer traceability backfill';
                v_total int;
                v_mapped int;
                v_configs int := 0;
                v_unmapped jsonb;
                v_by_source jsonb;
            BEGIN
                CREATE TEMP TABLE tmp_prep ON COMMIT DROP AS
                SELECT x.config_id, x.role, x.raw, x.txt,
                       (SELECT count(*) FROM "MediaProducts" p WHERE p."IsActive" AND (lower(btrim(p."Name")) = x.txt OR lower(btrim(p."Code")) = x.txt)) AS n_media,
                       (SELECT min(p."Id") FROM "MediaProducts" p WHERE p."IsActive" AND (lower(btrim(p."Name")) = x.txt OR lower(btrim(p."Code")) = x.txt)) AS media_id,
                       (SELECT count(*) FROM "SolutionMasters" m WHERE m."IsActive" AND m."Type" IN (1, 3, 4) AND lower(btrim(m."Name")) = x.txt) AS n_solution,
                       (SELECT min(m."Id") FROM "SolutionMasters" m WHERE m."IsActive" AND m."Type" IN (1, 3, 4) AND lower(btrim(m."Name")) = x.txt) AS solution_id,
                       (SELECT count(*) FROM "MaterialMasterEntries" e WHERE e."IsActive" AND (lower(btrim(e."Name")) = x.txt OR lower(btrim(e."Code")) = x.txt)) AS n_entry,
                       (SELECT min(e."Id") FROM "MaterialMasterEntries" e WHERE e."IsActive" AND (lower(btrim(e."Name")) = x.txt OR lower(btrim(e."Code")) = x.txt)) AS entry_id
                FROM (
                    SELECT c."Id" AS config_id, 'Diluent' AS role, c."Diluent" AS raw, lower(btrim(c."Diluent")) AS txt
                    FROM "ItemPreparationConfigurations" c WHERE c."DiluentSource" IS NULL
                    UNION ALL
                    SELECT c."Id", 'Neutralizer', c."Neutralizer", lower(btrim(c."Neutralizer"))
                    FROM "ItemPreparationConfigurations" c WHERE c."NeutralizerSource" IS NULL
                ) x;

                CREATE TEMP TABLE tmp_res ON COMMIT DROP AS
                SELECT t.config_id, t.role, t.raw,
                       CASE WHEN t.role = 'Neutralizer' AND t.txt IN ('none', 'n/a', 'na', '-', 'contained in medium', 'contained in the medium') THEN 3
                            WHEN t.n_media > 0 THEN CASE WHEN t.n_media = 1 THEN 0 END
                            WHEN t.n_solution > 0 THEN CASE WHEN t.n_solution = 1 THEN 1 END
                            WHEN t.n_entry > 0 THEN CASE WHEN t.n_entry = 1 THEN 2 END
                       END AS source,
                       CASE WHEN t.role = 'Neutralizer' AND t.txt IN ('none', 'n/a', 'na', '-', 'contained in medium', 'contained in the medium') THEN NULL
                            WHEN t.n_media = 1 THEN t.media_id
                            WHEN t.n_media = 0 AND t.n_solution = 1 THEN t.solution_id
                            WHEN t.n_media = 0 AND t.n_solution = 0 AND t.n_entry = 1 THEN t.entry_id
                       END AS master_id,
                       CASE WHEN t.role = 'Neutralizer' AND t.txt IN ('none', 'n/a', 'na', '-', 'contained in medium', 'contained in the medium') THEN NULL
                            WHEN t.n_media > 1 OR (t.n_media = 0 AND t.n_solution > 1) OR (t.n_media = 0 AND t.n_solution = 0 AND t.n_entry > 1) THEN 'AmbiguousMatch'
                            WHEN t.n_media = 0 AND t.n_solution = 0 AND t.n_entry = 0 THEN 'NoMatch'
                       END AS unresolved_reason
                FROM tmp_prep t;

                UPDATE "ItemPreparationConfigurations" c SET
                    "DiluentSource" = r.source,
                    "DiluentMediaProductId" = CASE WHEN r.source = 0 THEN r.master_id END,
                    "DiluentSolutionMasterId" = CASE WHEN r.source = 1 THEN r.master_id END,
                    "DiluentMaterialMasterEntryId" = CASE WHEN r.source = 2 THEN r.master_id END
                FROM tmp_res r
                WHERE r.config_id = c."Id" AND r.role = 'Diluent' AND r.source IS NOT NULL;

                UPDATE "ItemPreparationConfigurations" c SET
                    "NeutralizerSource" = r.source,
                    "NeutralizerMediaProductId" = CASE WHEN r.source = 0 THEN r.master_id END,
                    "NeutralizerSolutionMasterId" = CASE WHEN r.source = 1 THEN r.master_id END,
                    "NeutralizerMaterialMasterEntryId" = CASE WHEN r.source = 2 THEN r.master_id END
                FROM tmp_res r
                WHERE r.config_id = c."Id" AND r.role = 'Neutralizer' AND r.source IS NOT NULL;

                -- One audit row per configuration that got at least one mapping.
                WITH per_config AS (
                    SELECT r.config_id,
                           max(r.raw) FILTER (WHERE r.role = 'Diluent' AND r.source IS NOT NULL) AS d_raw,
                           max(r.source) FILTER (WHERE r.role = 'Diluent') AS d_source,
                           max(r.master_id) FILTER (WHERE r.role = 'Diluent') AS d_master,
                           max(r.raw) FILTER (WHERE r.role = 'Neutralizer' AND r.source IS NOT NULL) AS n_raw,
                           max(r.source) FILTER (WHERE r.role = 'Neutralizer') AS n_source,
                           max(r.master_id) FILTER (WHERE r.role = 'Neutralizer') AS n_master
                    FROM tmp_res r
                    GROUP BY r.config_id
                    HAVING count(*) FILTER (WHERE r.source IS NOT NULL) > 0
                ),
                audited AS (
                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    SELECT 'ItemPreparationConfiguration', k.config_id::text, 'Update',
                        (CASE WHEN k.d_source IS NOT NULL THEN jsonb_build_object('Diluent', k.d_raw, 'DiluentSource', NULL) ELSE '{}'::jsonb END
                         || CASE WHEN k.n_source IS NOT NULL THEN jsonb_build_object('Neutralizer', k.n_raw, 'NeutralizerSource', NULL) ELSE '{}'::jsonb END)::text,
                        (CASE WHEN k.d_source IS NOT NULL THEN jsonb_build_object(
                                'DiluentSource', CASE k.d_source WHEN 0 THEN 'MediaProduct' WHEN 1 THEN 'SolutionRecipe' ELSE 'MaterialEntry' END,
                                'DiluentMasterId', k.d_master) ELSE '{}'::jsonb END
                         || CASE WHEN k.n_source IS NOT NULL THEN jsonb_build_object(
                                'NeutralizerSource', CASE k.n_source WHEN 0 THEN 'MediaProduct' WHEN 1 THEN 'SolutionRecipe' WHEN 2 THEN 'MaterialEntry' ELSE 'ContainedInMedium' END,
                                'NeutralizerMasterId', k.n_master) ELSE '{}'::jsonb END)::text,
                        0, now(), 2, v_process, 'PreparationConfigurationMaterialsBackfilled', 6, v_reason
                    FROM per_config k
                    RETURNING 1
                )
                SELECT count(*) INTO v_configs FROM audited;

                SELECT count(*), count(*) FILTER (WHERE source IS NOT NULL) INTO v_total, v_mapped FROM tmp_res;

                SELECT COALESCE(jsonb_agg(jsonb_build_object('ConfigurationId', config_id, 'Role', role, 'Text', raw, 'Reason', unresolved_reason) ORDER BY config_id, role), '[]'::jsonb)
                INTO v_unmapped FROM tmp_res WHERE source IS NULL;

                SELECT jsonb_build_object(
                    'MediaProduct', count(*) FILTER (WHERE source = 0),
                    'SolutionRecipe', count(*) FILTER (WHERE source = 1),
                    'MaterialEntry', count(*) FILTER (WHERE source = 2),
                    'ContainedInMedium', count(*) FILTER (WHERE source = 3))
                INTO v_by_source FROM tmp_res;

                -- One summary row: the run's report.
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                VALUES ('ItemPreparationConfigurationBackfill', 'PreparationMaterialTraceability', 'Update', NULL,
                    jsonb_build_object(
                        'ConfigurationsMapped', v_configs,
                        'MappedCount', v_mapped,
                        'MappedBySource', v_by_source,
                        'UnmappedCount', v_total - v_mapped,
                        'Unmapped', v_unmapped)::text,
                    0, now(), 2, v_process, 'PreparationConfigurationMaterialsBackfillSummary', 6, v_reason);

                RAISE NOTICE 'Diluent/neutralizer backfill: % of % values mapped on % configurations, % left for a Section Head',
                    v_mapped, v_total, v_configs, v_total - v_mapped;

                DROP TABLE tmp_prep;
                DROP TABLE tmp_res;
            END
            $backfill$;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only: the backfilled mappings are data and are audited under the migration's system
            // process name. The dropped DiluentTypes table comes back empty (it was unused).
            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_MaterialMasterEntries_Diluent~",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_MaterialMasterEntries_Neutral~",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_MediaProducts_DiluentMediaPro~",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_MediaProducts_NeutralizerMedi~",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_SolutionMasters_DiluentSoluti~",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemPreparationConfigurations_SolutionMasters_NeutralizerSo~",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_Materials_DiluentMaterialId",
                table: "SamplePreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_Materials_NeutralizerMaterialId",
                table: "SamplePreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_Media_DiluentMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_Media_NeutralizerMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_SolutionPreparations_DiluentSolutionPrep~",
                table: "SamplePreparations");

            migrationBuilder.DropForeignKey(
                name: "FK_SamplePreparations_SolutionPreparations_NeutralizerSolution~",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_DiluentMaterialId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_DiluentMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_DiluentSolutionPreparationId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_NeutralizerMaterialId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_NeutralizerMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_SamplePreparations_NeutralizerSolutionPreparationId",
                table: "SamplePreparations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_DiluentMaterialMasterEntryId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_DiluentMediaProductId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_DiluentSolutionMasterId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerMaterialMasterEntr~",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerMediaProductId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_ItemPreparationConfigurations_NeutralizerSolutionMasterId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "DiluentLotCode",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "DiluentMaterialId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "DiluentMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "DiluentSolutionPreparationId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "DiluentSource",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "NeutralizerLotCode",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "NeutralizerMaterialId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "NeutralizerMediaId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "NeutralizerSolutionPreparationId",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "NeutralizerSource",
                table: "SamplePreparations");

            migrationBuilder.DropColumn(
                name: "DiluentMaterialMasterEntryId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "DiluentMediaProductId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "DiluentSolutionMasterId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "DiluentSource",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "NeutralizerMaterialMasterEntryId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "NeutralizerMediaProductId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "NeutralizerSolutionMasterId",
                table: "ItemPreparationConfigurations");

            migrationBuilder.DropColumn(
                name: "NeutralizerSource",
                table: "ItemPreparationConfigurations");

            migrationBuilder.CreateTable(
                name: "DiluentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaProductId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RequiresBatchTracking = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiluentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiluentTypes_MediaProducts_MediaProductId",
                        column: x => x.MediaProductId,
                        principalTable: "MediaProducts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiluentTypes_MediaProductId",
                table: "DiluentTypes",
                column: "MediaProductId");
        }
    }
}
