using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    // Hand-edited after scaffolding: the EF-generated Up() dropped the old
    // OrganismName columns before any OrganismId backfill, which would
    // have silently discarded the real MediaChallengeSpecs data already
    // entered. This version adds OrganismId as nullable first, backfills
    // it from the existing free-text values (normalizing known casing/
    // spelling issues and splitting embedded "#NNNNN" ATCC numbers into
    // Organisms.AtccNumber), verifies nothing is left unresolved, THEN
    // tightens to NOT NULL and drops the old string columns. See the
    // conversation for the exact distinct-value review before this was
    // applied.
    public partial class AddOrganismMasterAndSwapOrganismName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- 1. Organisms master table ----
            migrationBuilder.CreateTable(
                name: "Organisms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScientificName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AtccNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CommonName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Organisms_AtccNumber",
                table: "Organisms",
                column: "AtccNumber",
                unique: true,
                filter: "\"AtccNumber\" IS NOT NULL");

            // Case-insensitive uniqueness on ScientificName - not expressible
            // through EF's fluent API on Npgsql, so it's a raw expression index.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_Organisms_ScientificName_Lower\" ON \"Organisms\" ((lower(\"ScientificName\")));");

            // ---- 2. Add OrganismId as NULLABLE everywhere first (backfilled below, tightened to NOT NULL at the end where required) ----
            migrationBuilder.AddColumn<int>(name: "OrganismId", table: "MediaChallengeSpecs", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "OrganismId", table: "MediaEvaluationChallenges", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "OrganismId", table: "Cryovials", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "OrganismId", table: "Materials", type: "integer", nullable: true);

            migrationBuilder.RenameColumn(name: "OrganismName", table: "Cryovials", newName: "OrganismNameSnapshot");

            // ---- 3. Normalization helpers (dropped again at the end of this migration) ----
            // Strips a trailing "#12345" ATCC suffix into its own capture,
            // then applies the two known bad values from the real data
            // (casing on "staphylococcus aureus", spelling of "cepecia").
            migrationBuilder.Sql(@"
                CREATE FUNCTION microlims_normalize_organism_name(raw text) RETURNS text AS $$
                    SELECT CASE trim(regexp_replace(raw, '\s*#\d+\s*$', ''))
                        WHEN 'staphylococcus aureus' THEN 'Staphylococcus aureus'
                        WHEN 'Burkholderia cepecia' THEN 'Burkholderia cepacia'
                        ELSE trim(regexp_replace(raw, '\s*#\d+\s*$', ''))
                    END;
                $$ LANGUAGE sql IMMUTABLE;
            ");
            migrationBuilder.Sql(@"
                CREATE FUNCTION microlims_extract_organism_atcc(raw text) RETURNS text AS $$
                    SELECT (regexp_match(raw, '#(\d+)\s*$'))[1];
                $$ LANGUAGE sql IMMUTABLE;
            ");

            // ---- 4. Insert every distinct normalized organism found across the three tables ----
            migrationBuilder.Sql(@"
                INSERT INTO ""Organisms"" (""ScientificName"", ""AtccNumber"")
                SELECT DISTINCT microlims_normalize_organism_name(""OrganismName""), microlims_extract_organism_atcc(""OrganismName"")
                FROM ""MediaChallengeSpecs""
                ON CONFLICT (lower(""ScientificName"")) DO NOTHING;
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""Organisms"" (""ScientificName"", ""AtccNumber"")
                SELECT DISTINCT microlims_normalize_organism_name(""OrganismName""), microlims_extract_organism_atcc(""OrganismName"")
                FROM ""MediaEvaluationChallenges""
                ON CONFLICT (lower(""ScientificName"")) DO NOTHING;
            ");
            migrationBuilder.Sql(@"
                INSERT INTO ""Organisms"" (""ScientificName"", ""AtccNumber"")
                SELECT DISTINCT microlims_normalize_organism_name(""OrganismNameSnapshot""),
                       COALESCE(microlims_extract_organism_atcc(""OrganismNameSnapshot""), ""AtccNumber"")
                FROM ""Cryovials""
                ON CONFLICT (lower(""ScientificName"")) DO NOTHING;
            ");

            // ---- 5. Backfill OrganismId on every row by matching the normalized name ----
            migrationBuilder.Sql(@"
                UPDATE ""MediaChallengeSpecs"" s
                SET ""OrganismId"" = o.""Id""
                FROM ""Organisms"" o
                WHERE lower(o.""ScientificName"") = lower(microlims_normalize_organism_name(s.""OrganismName""));
            ");
            migrationBuilder.Sql(@"
                UPDATE ""MediaEvaluationChallenges"" c
                SET ""OrganismId"" = o.""Id""
                FROM ""Organisms"" o
                WHERE lower(o.""ScientificName"") = lower(microlims_normalize_organism_name(c.""OrganismName""));
            ");
            migrationBuilder.Sql(@"
                UPDATE ""Cryovials"" c
                SET ""OrganismId"" = o.""Id""
                FROM ""Organisms"" o
                WHERE lower(o.""ScientificName"") = lower(microlims_normalize_organism_name(c.""OrganismNameSnapshot""));
            ");

            // Best-effort only (Materials.OrganismId stays nullable either
            // way) - links existing LyophilizedMicroorganism rows whose
            // MaterialName happens to match a normalized organism name, so
            // Cryovial prep isn't blocked for stock received before this
            // migration. Anything left null just needs an Organism picked
            // on the Materials Stock page once.
            migrationBuilder.Sql(@"
                UPDATE ""Materials"" m
                SET ""OrganismId"" = o.""Id""
                FROM ""Organisms"" o
                WHERE m.""MaterialType"" = 1
                  AND lower(o.""ScientificName"") = lower(microlims_normalize_organism_name(m.""MaterialName""));
            ");

            // ---- 6. Fail loudly (not silently) if any row didn't resolve ----
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM ""MediaChallengeSpecs"" WHERE ""OrganismId"" IS NULL) THEN
                        RAISE EXCEPTION 'MediaChallengeSpecs has rows with unresolved OrganismId after backfill';
                    END IF;
                    IF EXISTS (SELECT 1 FROM ""MediaEvaluationChallenges"" WHERE ""OrganismId"" IS NULL) THEN
                        RAISE EXCEPTION 'MediaEvaluationChallenges has rows with unresolved OrganismId after backfill';
                    END IF;
                    IF EXISTS (SELECT 1 FROM ""Cryovials"" WHERE ""OrganismId"" IS NULL) THEN
                        RAISE EXCEPTION 'Cryovials has rows with unresolved OrganismId after backfill';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql("DROP FUNCTION microlims_normalize_organism_name(text);");
            migrationBuilder.Sql("DROP FUNCTION microlims_extract_organism_atcc(text);");

            // ---- 7. Tighten to NOT NULL now that every row is backfilled ----
            migrationBuilder.AlterColumn<int>(name: "OrganismId", table: "MediaChallengeSpecs", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "OrganismId", table: "MediaEvaluationChallenges", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "OrganismId", table: "Cryovials", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            // ---- 8. Drop the now-superseded free-text columns ----
            migrationBuilder.DropIndex(
                name: "IX_MediaChallengeSpecs_MaterialName_EvaluationType_OrganismNam~",
                table: "MediaChallengeSpecs");
            migrationBuilder.DropColumn(name: "OrganismName", table: "MediaChallengeSpecs");
            migrationBuilder.DropColumn(name: "OrganismName", table: "MediaEvaluationChallenges");
            migrationBuilder.DropColumn(name: "AtccNumber", table: "Cryovials");

            // ---- 9. Indexes + FKs ----
            migrationBuilder.CreateIndex(
                name: "IX_MediaEvaluationChallenges_OrganismId",
                table: "MediaEvaluationChallenges",
                column: "OrganismId");

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
                name: "IX_Materials_OrganismId",
                table: "Materials",
                column: "OrganismId");

            migrationBuilder.CreateIndex(
                name: "IX_Cryovials_OrganismId",
                table: "Cryovials",
                column: "OrganismId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cryovials_Organisms_OrganismId",
                table: "Cryovials",
                column: "OrganismId",
                principalTable: "Organisms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_Organisms_OrganismId",
                table: "Materials",
                column: "OrganismId",
                principalTable: "Organisms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaChallengeSpecs_Organisms_OrganismId",
                table: "MediaChallengeSpecs",
                column: "OrganismId",
                principalTable: "Organisms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaEvaluationChallenges_Organisms_OrganismId",
                table: "MediaEvaluationChallenges",
                column: "OrganismId",
                principalTable: "Organisms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cryovials_Organisms_OrganismId",
                table: "Cryovials");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_Organisms_OrganismId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaChallengeSpecs_Organisms_OrganismId",
                table: "MediaChallengeSpecs");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaEvaluationChallenges_Organisms_OrganismId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropTable(
                name: "Organisms");

            migrationBuilder.DropIndex(
                name: "IX_MediaEvaluationChallenges_OrganismId",
                table: "MediaEvaluationChallenges");

            migrationBuilder.DropIndex(
                name: "IX_MediaChallengeSpecs_MaterialName_EvaluationType_OrganismId_~",
                table: "MediaChallengeSpecs");

            migrationBuilder.DropIndex(
                name: "IX_MediaChallengeSpecs_OrganismId",
                table: "MediaChallengeSpecs");

            migrationBuilder.DropIndex(
                name: "IX_Materials_OrganismId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Cryovials_OrganismId",
                table: "Cryovials");

            migrationBuilder.AddColumn<string>(
                name: "OrganismName",
                table: "MediaEvaluationChallenges",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrganismName",
                table: "MediaChallengeSpecs",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Best-effort restore of the string values from the FK being
            // dropped, so a rollback doesn't leave every row blank.
            migrationBuilder.Sql(@"
                UPDATE ""MediaChallengeSpecs"" s SET ""OrganismName"" = o.""ScientificName""
                FROM ""Organisms"" o WHERE o.""Id"" = s.""OrganismId"";
            ");
            migrationBuilder.Sql(@"
                UPDATE ""MediaEvaluationChallenges"" c SET ""OrganismName"" = o.""ScientificName""
                FROM ""Organisms"" o WHERE o.""Id"" = c.""OrganismId"";
            ");

            migrationBuilder.DropColumn(name: "OrganismId", table: "MediaEvaluationChallenges");
            migrationBuilder.DropColumn(name: "OrganismId", table: "MediaChallengeSpecs");
            migrationBuilder.DropColumn(name: "OrganismId", table: "Materials");
            migrationBuilder.DropColumn(name: "OrganismId", table: "Cryovials");

            migrationBuilder.RenameColumn(
                name: "OrganismNameSnapshot",
                table: "Cryovials",
                newName: "OrganismName");

            migrationBuilder.AddColumn<string>(
                name: "AtccNumber",
                table: "Cryovials",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaChallengeSpecs_MaterialName_EvaluationType_OrganismNam~",
                table: "MediaChallengeSpecs",
                columns: new[] { "MaterialName", "EvaluationType", "OrganismName", "ChallengeRole" },
                unique: true);
        }
    }
}
