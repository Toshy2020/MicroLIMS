using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaterialCategoriesAndOtherMaterials : Migration
    {
        // Two new enforced permissions - MaterialCategories.Manage (create and change material categories) and
        // Materials.Release (release a received stock lot for use) - granted to the built-in Section Head role
        // (Type 1 = SectionHead AND IsSystemRole), never to a role that also manages users or roles (segregation
        // of duties). Raw SQL bypasses RoleService, so the audit row and the PermissionVersion bump (which
        // invalidates issued tokens) are written here, as in DocumentReassignment.
        private const string Process = "Migration 20261028114334_MaterialCategoriesAndOtherMaterials";
        private const string GrantReason = "Section Heads maintain material categories and release stock lots (Master Testing Materials phase 2)";

        private const string Declarations = """
            DECLARE
                r RECORD;
                prev_codes text[];
                new_codes text[];
                granted text[];
                revoked text[];
            """;

        private const string SnapshotPrevious = """
                    SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), '{}') INTO prev_codes
                    FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                    WHERE rp."RoleId" = r."Id";
            """;

        private const string AuditAndBump = """
                    SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), '{}') INTO new_codes
                    FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                    WHERE rp."RoleId" = r."Id";
                    granted := ARRAY(SELECT unnest(new_codes) EXCEPT SELECT unnest(prev_codes));
                    revoked := ARRAY(SELECT unnest(prev_codes) EXCEPT SELECT unnest(new_codes));
                    IF cardinality(granted) > 0 OR cardinality(revoked) > 0 THEN
                        UPDATE "Roles" SET "PermissionVersion" = "PermissionVersion" + 1 WHERE "Id" = r."Id";
                        INSERT INTO "AuditLogs" (
                            "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                            "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                        VALUES (
                            'Role', r."Id"::text, 'ROLE_PERMISSIONS_UPDATED',
                            jsonb_build_object('PermissionCodes', to_jsonb(prev_codes))::text,
                            jsonb_build_object('PermissionCodes', to_jsonb(new_codes), 'Granted', to_jsonb(granted), 'Revoked', to_jsonb(revoked))::text,
                            0, now(), 2, '{PROCESS}', 'RolePermissionsUpdated', 7, '{REASON}');
                    END IF;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Materials",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Materials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "Materials",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageCondition",
                table: "Materials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaterialCategoryId",
                table: "MaterialMasterEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CodePrefix = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DefaultUnit = table.Column<int>(type: "integer", nullable: false),
                    CoaRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiryRequired = table.Column<bool>(type: "boolean", nullable: false),
                    QuantityTracked = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultStorageCondition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovalBeforeUse = table.Column<bool>(type: "boolean", nullable: false),
                    LabelRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialCategorySections",
                columns: table => new
                {
                    MaterialCategoryId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialCategorySections", x => new { x.MaterialCategoryId, x.SectionId });
                    table.ForeignKey(
                        name: "FK_MaterialCategorySections_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialCategorySections_MaterialCategories_MaterialCategor~",
                        column: x => x.MaterialCategoryId,
                        principalTable: "MaterialCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialMasterEntries_MaterialCategoryId",
                table: "MaterialMasterEntries",
                column: "MaterialCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialCategories_CodePrefix",
                table: "MaterialCategories",
                column: "CodePrefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialCategories_Name",
                table: "MaterialCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialCategorySections_SectionId",
                table: "MaterialCategorySections",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialMasterEntries_MaterialCategories_MaterialCategoryId",
                table: "MaterialMasterEntries",
                column: "MaterialCategoryId",
                principalTable: "MaterialCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT v.code, v.descr, TRUE
                FROM (VALUES
                    ('MaterialCategories.Manage', 'Create and change material categories.'),
                    ('Materials.Release', 'Release a received stock lot for use.')) AS v(code, descr)
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" p WHERE p."Code" = v.code);
                """);
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" IN ('MaterialCategories.Manage', 'Materials.Release');""");

            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT ro."Id" FROM "Roles" ro
                         WHERE ro."Type" = 1 AND ro."IsSystemRole"
                           AND NOT EXISTS (
                               SELECT 1 FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                               WHERE rp."RoleId" = ro."Id" AND p."Code" IN ('Users.Manage', 'Roles.Manage')) LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" IN ('MaterialCategories.Manage', 'Materials.Release')
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", Process).Replace("{REASON}", GrantReason) +
                "\nEND LOOP;\nEND $$;");

            // Backfill (Master Testing Materials phase 2, one audited run, no review step): categories from the
            // legacy types and custom types, entries from the stock, links for unlinked lots. Lot snapshots
            // (MaterialType, MaterialName, Code) are never changed.
            migrationBuilder.Sql("""
            DO $backfill$
            DECLARE
                v_process constant text := 'Migration 20261028114334_MaterialCategoriesAndOtherMaterials';
                v_reason constant text := 'Master Testing Materials backfill';
                v_micro int;
                s record;
                c record;
                g record;
                v_id int;
                v_prefix text;
                v_base text;
                v_digit int;
                v_n int;
                v_code text;
                v_cur_cat int := NULL;
                v_categories int := 0;
                v_entries int := 0;
                v_linked_other int := 0;
                v_linked_reagent int := 0;
                v_unmatched jsonb;
                v_per_type jsonb;
            BEGIN
                SELECT "Id" INTO v_micro FROM "DocumentSections" WHERE "Code" = 'MICRO' ORDER BY "Id" LIMIT 1;

                -- type: the legacy MaterialType value (10 = Other, matched by norm = normalised CustomType; '' = no CustomType).
                CREATE TEMP TABLE tmp_cat (type int NOT NULL, norm text NOT NULL, cat_id int NOT NULL) ON COMMIT DROP;

                -- a. One category per legacy named type, plus General for Other lots without a custom type.
                FOR s IN
                    SELECT * FROM (VALUES
                        (2,  'Supplement',             'SUP', 2, true,  true,  true,  true),
                        (3,  'Antibiotic Disc',        'ABD', 4, false, true,  true,  true),
                        (4,  'Identification Kit',     'IDK', 6, false, true,  true,  true),
                        (5,  'Identification Reagent', 'IDR', 2, false, true,  true,  true),
                        (8,  'Reference Buffer',       'BUF', 8, false, true,  true,  false),
                        (9,  'Disposable Tool',        'DSP', 7, false, false, false, false),
                        (10, 'General',                'GEN', 7, false, true,  true,  false)
                    ) AS t(type, name, prefix, unit, coa, expiry, qty, micro_only)
                    ORDER BY type
                LOOP
                    INSERT INTO "MaterialCategories" (
                        "Name", "CodePrefix", "DefaultUnit", "CoaRequired", "ExpiryRequired", "QuantityTracked",
                        "DefaultStorageCondition", "ApprovalBeforeUse", "LabelRequired", "IsActive",
                        "CreatedByUserId", "CreatedAt", "LastModifiedByUserId", "LastModifiedAt")
                    VALUES (s.name, s.prefix, s.unit, s.coa, s.expiry, s.qty, NULL, false, false, true, 0, now(), 0, now())
                    RETURNING "Id" INTO v_id;

                    IF s.micro_only AND v_micro IS NOT NULL THEN
                        INSERT INTO "MaterialCategorySections" ("MaterialCategoryId", "SectionId") VALUES (v_id, v_micro);
                    END IF;

                    INSERT INTO tmp_cat VALUES (s.type, '', v_id);
                    v_categories := v_categories + 1;

                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    VALUES ('MaterialCategory', v_id::text, 'Create', NULL,
                        json_build_object('Name', s.name, 'CodePrefix', s.prefix, 'DefaultUnit', s.unit, 'CoaRequired', s.coa,
                            'ExpiryRequired', s.expiry, 'QuantityTracked', s.qty, 'ApprovalBeforeUse', false, 'LabelRequired', false,
                            'Sections', CASE WHEN s.micro_only AND v_micro IS NOT NULL THEN to_json(ARRAY[v_micro]) ELSE '[]'::json END)::text,
                        0, now(), 2, v_process, 'MaterialCategoryBackfilled', 6, v_reason);
                END LOOP;

                -- b. One category per distinct normalised CustomType on unlinked Other lots. Name = the most common original
                -- spelling; prefix = first three letters/digits of it, with a digit appended until unique (the prefixes of the
                -- other master groups are taken too). A name equal to an existing category (e.g. General) reuses it.
                FOR c IN
                    WITH lots AS (
                        SELECT btrim(regexp_replace("CustomType", '\s+', ' ', 'g')) AS orig,
                               lower(btrim(regexp_replace("CustomType", '\s+', ' ', 'g'))) AS norm
                        FROM "Materials"
                        WHERE "MaterialType" = 10 AND "MaterialMasterEntryId" IS NULL AND btrim(coalesce("CustomType", '')) <> ''
                    ),
                    spell AS (SELECT norm, orig, count(*) AS n FROM lots GROUP BY norm, orig),
                    best AS (SELECT DISTINCT ON (norm) norm, orig FROM spell ORDER BY norm, n DESC, orig)
                    SELECT norm, left(orig, 100) AS name FROM best ORDER BY norm
                LOOP
                    SELECT "Id" INTO v_id FROM "MaterialCategories" WHERE lower("Name") = lower(c.name) ORDER BY "Id" LIMIT 1;

                    IF v_id IS NULL THEN
                        v_base := left(regexp_replace(upper(c.name), '[^A-Z0-9]', '', 'g'), 3);
                        IF length(v_base) < 2 THEN v_base := rpad(v_base, 2, 'X'); END IF;
                        v_prefix := v_base;
                        v_digit := 1;
                        WHILE v_prefix = ANY (ARRAY['RG', 'IND', 'RS', 'PS', 'ORG', 'WS'])
                              OR EXISTS (SELECT 1 FROM "MaterialCategories" WHERE "CodePrefix" = v_prefix) LOOP
                            v_digit := v_digit + 1;
                            v_prefix := v_base || v_digit::text;
                        END LOOP;

                        INSERT INTO "MaterialCategories" (
                            "Name", "CodePrefix", "DefaultUnit", "CoaRequired", "ExpiryRequired", "QuantityTracked",
                            "DefaultStorageCondition", "ApprovalBeforeUse", "LabelRequired", "IsActive",
                            "CreatedByUserId", "CreatedAt", "LastModifiedByUserId", "LastModifiedAt")
                        VALUES (c.name, v_prefix, 7, false, true, true, NULL, false, false, true, 0, now(), 0, now())
                        RETURNING "Id" INTO v_id;
                        v_categories := v_categories + 1;

                        INSERT INTO "AuditLogs" (
                            "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                            "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                        VALUES ('MaterialCategory', v_id::text, 'Create', NULL,
                            json_build_object('Name', c.name, 'CodePrefix', v_prefix, 'DefaultUnit', 7, 'CoaRequired', false,
                                'ExpiryRequired', true, 'QuantityTracked', true, 'ApprovalBeforeUse', false, 'LabelRequired', false,
                                'Sections', '[]'::json)::text,
                            0, now(), 2, v_process, 'MaterialCategoryBackfilled', 6, v_reason);
                    END IF;

                    INSERT INTO tmp_cat VALUES (10, c.norm, v_id);
                END LOOP;

                -- c. Group the unlinked lots of the seven legacy types by (target category, normalised name).
                CREATE TEMP TABLE tmp_lot ON COMMIT DROP AS
                SELECT m."Id" AS lot_id, m."BatchNumber" AS batch, m."SectionId" AS section_id, m."Unit" AS unit,
                       m."ReceivingDate" AS received,
                       COALESCE(NULLIF(btrim(m."MaterialName"), ''), 'Unnamed material') AS name_raw,
                       lower(regexp_replace(COALESCE(NULLIF(btrim(m."MaterialName"), ''), 'Unnamed material'), '\s+', ' ', 'g')) AS norm_name,
                       CASE WHEN m."MaterialType" = 10 THEN
                            COALESCE(
                                (SELECT t.cat_id FROM tmp_cat t WHERE t.type = 10 AND t.norm <> ''
                                    AND t.norm = lower(btrim(regexp_replace(COALESCE(m."CustomType", ''), '\s+', ' ', 'g')))),
                                (SELECT t.cat_id FROM tmp_cat t WHERE t.type = 10 AND t.norm = ''))
                            ELSE (SELECT t.cat_id FROM tmp_cat t WHERE t.type = m."MaterialType")
                       END AS cat_id
                FROM "Materials" m
                WHERE m."MaterialType" IN (2, 3, 4, 5, 8, 9, 10) AND m."MaterialMasterEntryId" IS NULL;

                CREATE TEMP TABLE tmp_group (cat_id int NOT NULL, norm_name text NOT NULL, entry_id int NOT NULL) ON COMMIT DROP;

                FOR g IN
                    SELECT cat_id, norm_name,
                           (array_agg(left(name_raw, 200) ORDER BY received, lot_id))[1] AS name,
                           (array_agg(section_id ORDER BY received, lot_id))[1] AS section_id,
                           (array_agg(unit ORDER BY received, lot_id))[1] AS unit
                    FROM tmp_lot
                    GROUP BY cat_id, norm_name
                    ORDER BY cat_id, norm_name
                LOOP
                    IF v_cur_cat IS DISTINCT FROM g.cat_id THEN
                        v_cur_cat := g.cat_id;
                        v_n := 0;
                        SELECT "CodePrefix" INTO v_prefix FROM "MaterialCategories" WHERE "Id" = g.cat_id;
                    END IF;

                    LOOP
                        v_n := v_n + 1;
                        v_code := v_prefix || '-' || repeat('0', greatest(3 - length(v_n::text), 0)) || v_n::text;
                        EXIT WHEN NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" WHERE "Code" = v_code);
                    END LOOP;

                    INSERT INTO "MaterialMasterEntries" (
                        "SectionId", "Code", "Name", "Category", "BaseUnit", "IsActive", "MaterialCategoryId",
                        "CreatedByUserId", "CreatedAt", "LastModifiedByUserId", "LastModifiedAt")
                    VALUES (g.section_id, v_code, g.name, 4, g.unit, true, g.cat_id, 0, now(), 0, now())
                    RETURNING "Id" INTO v_id;
                    v_entries := v_entries + 1;

                    INSERT INTO tmp_group VALUES (g.cat_id, g.norm_name, v_id);

                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    VALUES ('MaterialMasterEntry', v_id::text, 'Create', NULL,
                        json_build_object('Code', v_code, 'Name', g.name, 'Category', 4, 'MaterialCategoryId', g.cat_id,
                            'SectionId', g.section_id, 'BaseUnit', g.unit)::text,
                        0, now(), 2, v_process, 'MaterialMasterEntryBackfilled', 6, v_reason);
                END LOOP;

                -- Link the lots. MaterialType, MaterialName and Code are snapshots and stay as received.
                WITH linked AS (
                    UPDATE "Materials" m SET "MaterialMasterEntryId" = gm.entry_id
                    FROM tmp_lot l
                    JOIN tmp_group gm ON gm.cat_id = l.cat_id AND gm.norm_name = l.norm_name
                    WHERE m."Id" = l.lot_id
                    RETURNING m."Id", m."BatchNumber", gm.entry_id
                ),
                audited AS (
                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp", "BatchNumber",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    SELECT 'Material', k."Id"::text, 'Update',
                        json_build_object('MaterialMasterEntryId', NULL)::text,
                        json_build_object('MaterialMasterEntryId', k.entry_id)::text,
                        0, now(), k."BatchNumber", 2, v_process, 'MaterialLinkedToMasterEntry', 6, v_reason
                    FROM linked k
                    RETURNING 1
                )
                SELECT count(*) INTO v_linked_other FROM audited;

                -- d. Unlinked reagent / indicator / standard lots: link to the existing entry of the matching category when the
                -- lot's Code equals the entry code, else when the normalised name matches exactly one entry of that category.
                -- Unmatched lots stay unlinked (listed in the summary row).
                CREATE TEMP TABLE tmp_link ON COMMIT DROP AS
                SELECT x.lot_id, x.batch,
                       COALESCE(
                           (SELECT e."Id" FROM "MaterialMasterEntries" e
                            WHERE e."Category" = x.cat AND x.code IS NOT NULL AND e."Code" = x.code),
                           (SELECT min(e."Id") FROM "MaterialMasterEntries" e
                            WHERE e."Category" = x.cat
                              AND lower(btrim(regexp_replace(e."Name", '\s+', ' ', 'g'))) = x.norm
                            HAVING count(*) = 1)) AS entry_id
                FROM (
                    SELECT m."Id" AS lot_id, m."BatchNumber" AS batch,
                           CASE m."MaterialType" WHEN 6 THEN 0 WHEN 7 THEN 1 WHEN 11 THEN 2 ELSE 3 END AS cat,
                           NULLIF(upper(btrim(m."Code")), '') AS code,
                           lower(btrim(regexp_replace(m."MaterialName", '\s+', ' ', 'g'))) AS norm
                    FROM "Materials" m
                    WHERE m."MaterialType" IN (6, 7, 11, 13) AND m."MaterialMasterEntryId" IS NULL
                ) x;

                WITH linked AS (
                    UPDATE "Materials" m SET "MaterialMasterEntryId" = k.entry_id
                    FROM tmp_link k
                    WHERE m."Id" = k.lot_id AND k.entry_id IS NOT NULL
                    RETURNING m."Id", m."BatchNumber", k.entry_id
                ),
                audited AS (
                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp", "BatchNumber",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    SELECT 'Material', k."Id"::text, 'Update',
                        json_build_object('MaterialMasterEntryId', NULL)::text,
                        json_build_object('MaterialMasterEntryId', k.entry_id)::text,
                        0, now(), k."BatchNumber", 2, v_process, 'MaterialLinkedToMasterEntry', 6, v_reason
                    FROM linked k
                    RETURNING 1
                )
                SELECT count(*) INTO v_linked_reagent FROM audited;

                SELECT COALESCE(jsonb_agg(lot_id ORDER BY lot_id), '[]'::jsonb) INTO v_unmatched FROM tmp_link WHERE entry_id IS NULL;

                SELECT COALESCE(jsonb_object_agg(type::text, n), '{}'::jsonb) INTO v_per_type
                FROM (SELECT m."MaterialType" AS type, count(*) AS n FROM "Materials" m
                      WHERE m."Id" IN (SELECT lot_id FROM tmp_lot) GROUP BY m."MaterialType") q;

                -- One summary row: the run's report.
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                VALUES ('MaterialBackfill', 'MasterTestingMaterials', 'Update', NULL,
                    jsonb_build_object(
                        'CategoriesCreated', v_categories,
                        'EntriesCreated', v_entries,
                        'OtherLotsLinked', v_linked_other,
                        'OtherLotsLinkedByLegacyType', v_per_type,
                        'ReagentAndStandardLotsLinked', v_linked_reagent,
                        'ReagentAndStandardLotsUnmatchedCount', jsonb_array_length(v_unmatched),
                        'ReagentAndStandardLotsUnmatched', v_unmatched)::text,
                    0, now(), 2, v_process, 'MaterialBackfillSummary', 6, v_reason);

                RAISE NOTICE 'Master Testing Materials backfill: % categories, % entries, % other lots linked, % reagent/standard lots linked, % unmatched',
                    v_categories, v_entries, v_linked_other, v_linked_reagent, jsonb_array_length(v_unmatched);

                DROP TABLE tmp_link;
                DROP TABLE tmp_group;
                DROP TABLE tmp_lot;
                DROP TABLE tmp_cat;
            END
            $backfill$;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                $$"""
                FOR r IN SELECT DISTINCT a."EntityId"::int AS "Id" FROM "AuditLogs" a
                         WHERE a."SystemProcessName" = '{{Process}}' AND a."ActionCode" = 'RolePermissionsUpdated' LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" IN ('MaterialCategories.Manage', 'Materials.Release'));

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", Process + " (Down)").Replace("{REASON}", "Material category and release grants rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" IN ('MaterialCategories.Manage', 'Materials.Release'));
                """);
            migrationBuilder.Sql("""DELETE FROM "Permissions" WHERE "Code" IN ('MaterialCategories.Manage', 'Materials.Release');""");

            // Schema only: the backfilled categories, entries and lot links are data and are not reversed
            // (they are audited under the migration's system process name).
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialMasterEntries_MaterialCategories_MaterialCategoryId",
                table: "MaterialMasterEntries");

            migrationBuilder.DropTable(
                name: "MaterialCategorySections");

            migrationBuilder.DropTable(
                name: "MaterialCategories");

            migrationBuilder.DropIndex(
                name: "IX_MaterialMasterEntries_MaterialCategoryId",
                table: "MaterialMasterEntries");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "StorageCondition",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "MaterialCategoryId",
                table: "MaterialMasterEntries");
        }
    }
}
