using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceOrganisms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "MediaProducts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceOrganismId",
                table: "Materials",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceOrganismId",
                table: "Cryovials",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReferenceOrganisms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrganismId = table.Column<int>(type: "integer", nullable: false),
                    CultureCollection = table.Column<int>(type: "integer", nullable: false),
                    StrainNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OwnerSectionId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceOrganisms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferenceOrganisms_DocumentSections_OwnerSectionId",
                        column: x => x.OwnerSectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferenceOrganisms_Organisms_OrganismId",
                        column: x => x.OrganismId,
                        principalTable: "Organisms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materials_ReferenceOrganismId",
                table: "Materials",
                column: "ReferenceOrganismId");

            migrationBuilder.CreateIndex(
                name: "IX_Cryovials_ReferenceOrganismId",
                table: "Cryovials",
                column: "ReferenceOrganismId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceOrganisms_Code",
                table: "ReferenceOrganisms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceOrganisms_CultureCollection_StrainNumber",
                table: "ReferenceOrganisms",
                columns: new[] { "CultureCollection", "StrainNumber" },
                unique: true,
                filter: "\"StrainNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceOrganisms_OrganismId",
                table: "ReferenceOrganisms",
                column: "OrganismId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceOrganisms_OwnerSectionId",
                table: "ReferenceOrganisms",
                column: "OwnerSectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cryovials_ReferenceOrganisms_ReferenceOrganismId",
                table: "Cryovials",
                column: "ReferenceOrganismId",
                principalTable: "ReferenceOrganisms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_ReferenceOrganisms_ReferenceOrganismId",
                table: "Materials",
                column: "ReferenceOrganismId",
                principalTable: "ReferenceOrganisms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill (Master Testing Materials phase 3, one audited run, no review step): a reference strain per
            // organism with an ATCC number, links for lyophilized lots and their cryovials. Lot snapshots
            // (MaterialName, Code, AtccNumber) and cryovial codes are never changed.
            migrationBuilder.Sql("""
            DO $backfill$
            DECLARE
                v_process constant text := 'Migration 20261028123607_ReferenceOrganisms';
                v_reason constant text := 'Master Testing Materials backfill';
                v_owner int;
                o record;
                v_id int;
                v_n int := 0;
                v_code text;
                v_strains_atcc int := 0;
                v_strains_other int := 0;
                v_lots_by_organism int := 0;
                v_lots_by_atcc int := 0;
                v_lots_to_other int := 0;
                v_cryovials int := 0;
                v_lots_linked int := 0;
                v_unlinked_lots jsonb;
                v_unlinked_cryovials jsonb;
            BEGIN
                SELECT "Id" INTO v_owner FROM "DocumentSections" WHERE "Code" = 'MICRO' ORDER BY "Id" LIMIT 1;
                IF v_owner IS NULL THEN SELECT min("Id") INTO v_owner FROM "DocumentSections"; END IF;

                -- organism -> its strain; norm = the organism's ATCC number without an 'ATCC' prefix, upper-cased.
                CREATE TEMP TABLE tmp_strain (organism_id int PRIMARY KEY, strain_id int NOT NULL, collection int NOT NULL, norm text) ON COMMIT DROP;

                IF v_owner IS NOT NULL THEN
                    -- a. One ATCC strain per organism with an ATCC number, coded ORG-001.. in scientific-name order. Organisms
                    -- whose trimmed numbers collide keep the strain of the lowest id; the others are treated as having none.
                    FOR o IN
                        SELECT x."Id", x."ScientificName", x.strain_number
                        FROM (
                            SELECT DISTINCT ON (btrim(og."AtccNumber")) og."Id", og."ScientificName", btrim(og."AtccNumber") AS strain_number
                            FROM "Organisms" og
                            WHERE og."AtccNumber" IS NOT NULL AND btrim(og."AtccNumber") <> ''
                            ORDER BY btrim(og."AtccNumber"), og."Id"
                        ) x
                        ORDER BY x."ScientificName", x."Id"
                    LOOP
                        LOOP
                            v_n := v_n + 1;
                            v_code := 'ORG-' || repeat('0', greatest(3 - length(v_n::text), 0)) || v_n::text;
                            EXIT WHEN NOT EXISTS (SELECT 1 FROM "ReferenceOrganisms" WHERE "Code" = v_code);
                        END LOOP;

                        INSERT INTO "ReferenceOrganisms" (
                            "Code", "OrganismId", "CultureCollection", "StrainNumber", "OwnerSectionId", "IsActive",
                            "CreatedByUserId", "CreatedAt", "LastModifiedByUserId", "LastModifiedAt")
                        VALUES (v_code, o."Id", 0, o.strain_number, v_owner, true, 0, now(), 0, now())
                        RETURNING "Id" INTO v_id;
                        v_strains_atcc := v_strains_atcc + 1;

                        INSERT INTO tmp_strain VALUES (o."Id", v_id, 0,
                            btrim(regexp_replace(upper(o.strain_number), '^ATCC[\s\-]*', '')));

                        INSERT INTO "AuditLogs" (
                            "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                            "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                        VALUES ('ReferenceOrganism', v_id::text, 'Create', NULL,
                            json_build_object('Code', v_code, 'OrganismId', o."Id", 'CultureCollection', 0,
                                'StrainNumber', o.strain_number, 'OwnerSectionId', v_owner)::text,
                            0, now(), 2, v_process, 'ReferenceOrganismBackfilled', 6, v_reason);
                    END LOOP;
                END IF;

                -- b. Unlinked lyophilized lots (MaterialType 1). Branch 1: the lot's organism has a strain. Branch 2: the lot
                -- has no organism and its ATCC number (trimmed, upper-cased, no leading 'ATCC') matches exactly one strain.
                -- Branch 3: the lot's organism has no strain - link a collection-Other strain (no number), one per organism.
                -- Otherwise the lot stays unlinked and is listed in the summary row.
                CREATE TEMP TABLE tmp_lot ON COMMIT DROP AS
                SELECT m."Id" AS lot_id, m."BatchNumber" AS batch, m."OrganismId" AS organism_id,
                       NULLIF(btrim(regexp_replace(upper(btrim(COALESCE(m."AtccNumber", ''))), '^ATCC[\s\-]*', '')), '') AS norm,
                       0 AS branch, NULL::int AS strain_id, NULL::int AS new_organism_id
                FROM "Materials" m
                WHERE m."MaterialType" = 1 AND m."ReferenceOrganismId" IS NULL AND v_owner IS NOT NULL;

                UPDATE tmp_lot l SET branch = 1, strain_id = s.strain_id
                FROM tmp_strain s WHERE l.organism_id = s.organism_id;

                UPDATE tmp_lot l SET branch = 2, strain_id = m.strain_id, new_organism_id = m.organism_id
                FROM (SELECT norm, min(strain_id) AS strain_id, min(organism_id) AS organism_id
                      FROM tmp_strain WHERE norm IS NOT NULL AND norm <> '' GROUP BY norm HAVING count(*) = 1) m
                WHERE l.branch = 0 AND l.organism_id IS NULL AND l.norm = m.norm;

                -- Branch 3: one Other strain per such organism, numbered after the ATCC strains.
                FOR o IN
                    SELECT og."Id", og."ScientificName"
                    FROM "Organisms" og
                    WHERE og."Id" IN (SELECT organism_id FROM tmp_lot WHERE branch = 0 AND organism_id IS NOT NULL)
                    ORDER BY og."ScientificName", og."Id"
                LOOP
                    LOOP
                        v_n := v_n + 1;
                        v_code := 'ORG-' || repeat('0', greatest(3 - length(v_n::text), 0)) || v_n::text;
                        EXIT WHEN NOT EXISTS (SELECT 1 FROM "ReferenceOrganisms" WHERE "Code" = v_code);
                    END LOOP;

                    INSERT INTO "ReferenceOrganisms" (
                        "Code", "OrganismId", "CultureCollection", "StrainNumber", "OwnerSectionId", "IsActive",
                        "CreatedByUserId", "CreatedAt", "LastModifiedByUserId", "LastModifiedAt")
                    VALUES (v_code, o."Id", 5, NULL, v_owner, true, 0, now(), 0, now())
                    RETURNING "Id" INTO v_id;
                    v_strains_other := v_strains_other + 1;

                    UPDATE tmp_lot SET branch = 3, strain_id = v_id WHERE branch = 0 AND organism_id = o."Id";

                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    VALUES ('ReferenceOrganism', v_id::text, 'Create', NULL,
                        json_build_object('Code', v_code, 'OrganismId', o."Id", 'CultureCollection', 5,
                            'StrainNumber', NULL, 'OwnerSectionId', v_owner)::text,
                        0, now(), 2, v_process, 'ReferenceOrganismBackfilled', 6, v_reason);
                END LOOP;

                -- Link the lots. MaterialName, Code and AtccNumber are snapshots and stay as received; OrganismId is
                -- set only when it was null (branch 2).
                WITH linked AS (
                    UPDATE "Materials" m SET "ReferenceOrganismId" = l.strain_id,
                                             "OrganismId" = COALESCE(m."OrganismId", l.new_organism_id)
                    FROM tmp_lot l
                    WHERE m."Id" = l.lot_id AND l.strain_id IS NOT NULL
                    RETURNING m."Id", m."BatchNumber", m."OrganismId", l.strain_id, l.branch, l.organism_id AS old_organism_id
                ),
                audited AS (
                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp", "BatchNumber",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    SELECT 'Material', k."Id"::text, 'Update',
                        json_build_object('ReferenceOrganismId', NULL, 'OrganismId', k.old_organism_id)::text,
                        json_build_object('ReferenceOrganismId', k.strain_id, 'OrganismId', k."OrganismId")::text,
                        0, now(), k."BatchNumber", 2, v_process, 'MaterialLinkedToReferenceOrganism', 6, v_reason
                    FROM linked k
                    RETURNING 1
                )
                SELECT count(*) INTO v_lots_linked FROM audited;

                SELECT count(*) FILTER (WHERE branch = 1), count(*) FILTER (WHERE branch = 2), count(*) FILTER (WHERE branch = 3)
                INTO v_lots_by_organism, v_lots_by_atcc, v_lots_to_other FROM tmp_lot;

                SELECT COALESCE(jsonb_agg(lot_id ORDER BY lot_id), '[]'::jsonb) INTO v_unlinked_lots FROM tmp_lot WHERE strain_id IS NULL;

                -- c. Cryovials take the strain of their source lot (when it has one and the species is the same).
                WITH linked AS (
                    UPDATE "Cryovials" c SET "ReferenceOrganismId" = m."ReferenceOrganismId"
                    FROM "Materials" m
                    JOIN "ReferenceOrganisms" r ON r."Id" = m."ReferenceOrganismId"
                    WHERE c."MaterialId" = m."Id" AND c."ReferenceOrganismId" IS NULL AND r."OrganismId" = c."OrganismId"
                    RETURNING c."Id", m."BatchNumber", m."ReferenceOrganismId" AS strain_id
                ),
                audited AS (
                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp", "BatchNumber",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    SELECT 'Cryovial', k."Id"::text, 'Update',
                        json_build_object('ReferenceOrganismId', NULL)::text,
                        json_build_object('ReferenceOrganismId', k.strain_id)::text,
                        0, now(), k."BatchNumber", 2, v_process, 'CryovialLinkedToReferenceOrganism', 6, v_reason
                    FROM linked k
                    RETURNING 1
                )
                SELECT count(*) INTO v_cryovials FROM audited;

                SELECT COALESCE(jsonb_agg(c."Id" ORDER BY c."Id"), '[]'::jsonb) INTO v_unlinked_cryovials
                FROM "Cryovials" c JOIN "Materials" m ON m."Id" = c."MaterialId"
                WHERE c."ReferenceOrganismId" IS NULL AND m."MaterialType" = 1;

                -- d. One summary row: the run's report.
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                VALUES ('ReferenceOrganismBackfill', 'MasterTestingMaterials', 'Update', NULL,
                    jsonb_build_object(
                        'AtccStrainsCreated', v_strains_atcc,
                        'OtherStrainsCreated', v_strains_other,
                        'LotsLinkedByOrganism', v_lots_by_organism,
                        'LotsLinkedByAtccNumber', v_lots_by_atcc,
                        'LotsLinkedToOtherStrain', v_lots_to_other,
                        'CryovialsLinked', v_cryovials,
                        'LotsUnlinkedCount', jsonb_array_length(v_unlinked_lots),
                        'LotsUnlinked', v_unlinked_lots,
                        'CryovialsUnlinkedCount', jsonb_array_length(v_unlinked_cryovials),
                        'CryovialsUnlinked', v_unlinked_cryovials)::text,
                    0, now(), 2, v_process, 'ReferenceOrganismBackfillSummary', 6, v_reason);

                RAISE NOTICE 'Reference organism backfill: % ATCC strains, % other strains, % lots linked (% by organism, % by ATCC number, % to other strain), % cryovials linked, % lots unlinked',
                    v_strains_atcc, v_strains_other, v_lots_linked,
                    v_lots_by_organism, v_lots_by_atcc, v_lots_to_other, v_cryovials, jsonb_array_length(v_unlinked_lots);

                DROP TABLE tmp_lot;
                DROP TABLE tmp_strain;
            END
            $backfill$;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only: the backfilled strains and links are data and are not reversed
            // (they are audited under the migration's system process name).
            migrationBuilder.DropForeignKey(
                name: "FK_Cryovials_ReferenceOrganisms_ReferenceOrganismId",
                table: "Cryovials");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_ReferenceOrganisms_ReferenceOrganismId",
                table: "Materials");

            migrationBuilder.DropTable(
                name: "ReferenceOrganisms");

            migrationBuilder.DropIndex(
                name: "IX_Materials_ReferenceOrganismId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Cryovials_ReferenceOrganismId",
                table: "Cryovials");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "MediaProducts");

            migrationBuilder.DropColumn(
                name: "ReferenceOrganismId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ReferenceOrganismId",
                table: "Cryovials");
        }
    }
}
