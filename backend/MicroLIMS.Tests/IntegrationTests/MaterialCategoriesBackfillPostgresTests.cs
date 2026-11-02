using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE MaterialCategoriesAndOtherMaterials, seed old-schema
// stock, migrate on, and check the audited backfill (categories, entries, links; lot snapshots untouched).
public class MaterialCategoriesBackfillPostgresTests
{
    private const string Before = "20261028111111_GlobalMaterialMasterCodes";
    private const string Target = "20261028114334_MaterialCategoriesAndOtherMaterials";
    private const string Process = "Migration " + Target;

    [PostgresFact]
    public async Task Migration_SeedsCategories_BuildsEntriesFromStock_AndLinksLots()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var cs = PostgresTestConfiguration.BuildTestConnectionString(admin, name);
        await Exec(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(cs).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            var micro = (int)await Scalar(c, "SELECT \"Id\" FROM \"DocumentSections\" WHERE \"Code\" = 'MICRO'");
            var fp = (int)await Scalar(c, "SELECT \"Id\" FROM \"DocumentSections\" WHERE \"Code\" = 'FP'");

            var rg1 = await Insert(c, "MaterialMasterEntries", ("SectionId", fp), ("Code", "RG-001"), ("Name", "Sodium Hydroxide AR"), ("Category", 0));
            var rg2 = await Insert(c, "MaterialMasterEntries", ("SectionId", fp), ("Code", "RG-002"), ("Name", "Potassium Chloride"), ("Category", 0));
            await Insert(c, "MaterialMasterEntries", ("SectionId", fp), ("Code", "RG-010"), ("Name", "Glycine"), ("Category", 0));
            await Insert(c, "MaterialMasterEntries", ("SectionId", micro), ("Code", "RG-011"), ("Name", "Glycine"), ("Category", 0));

            // Supplement lots: three spellings of one name, one different material.
            var s1 = await Lot(c, micro, 2, "Egg Yolk Tellurite", "2026-01-05", unit: 2);
            var s2 = await Lot(c, micro, 2, "egg yolk   tellurite ", "2026-02-01", unit: 2);
            var s3 = await Lot(c, micro, 2, "Egg Yolk Tellurite Emulsion", "2026-02-02", unit: 2);
            // The earliest lot decides the owner lab and the unit.
            var d1 = await Lot(c, fp, 9, "Gloves", "2026-03-01", unit: 7);
            var d2 = await Lot(c, micro, 9, "GLOVES", "2026-01-02", unit: 7);
            // Custom types: two spellings of one, one that would collide with the Indicator prefix, none, and General itself.
            var o1 = await Lot(c, fp, 10, "Methanol", "2026-01-01", customType: "Solvent");
            var o2 = await Lot(c, fp, 10, "Ethanol", "2026-01-02", customType: "solvent ");
            var o3 = await Lot(c, fp, 10, "Acetone", "2026-01-03", customType: "Solvent");
            var o4 = await Lot(c, fp, 10, "Test solution", "2026-01-04", customType: "Indicator solution");
            var o5 = await Lot(c, micro, 10, "Pipette tips", "2026-01-05");
            var o6 = await Lot(c, micro, 10, "Filter", "2026-01-06", customType: "General");
            // Reagent lots: by code, by name, ambiguous name, unknown.
            var byCode = await Lot(c, fp, 6, "whatever", "2026-01-03", code: "RG-001");
            var byName = await Lot(c, fp, 6, "potassium  chloride", "2026-01-03");
            var ambiguous = await Lot(c, fp, 6, "Glycine", "2026-01-03");
            var unknown = await Lot(c, fp, 6, "Unknown thing", "2026-01-03");
            var media = await Lot(c, micro, 0, "TSA powder", "2026-01-03", code: "TSA");

            await migrator.MigrateAsync(Target);

            // Categories: seven seeded + Solvent + Indicator solution (prefix IND is taken by indicators, so IND2); General is reused.
            Assert.Equal(9, await Scalar(c, "SELECT count(*) FROM \"MaterialCategories\""));
            Assert.Equal("SUP", await Text(c, "SELECT \"CodePrefix\" FROM \"MaterialCategories\" WHERE \"Name\" = 'Supplement'"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"MaterialCategories\" WHERE \"Name\" = 'Supplement' AND \"CoaRequired\" AND \"DefaultUnit\" = 2 AND \"ExpiryRequired\" AND \"QuantityTracked\""));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"MaterialCategories\" WHERE \"Name\" = 'Disposable Tool' AND NOT \"ExpiryRequired\" AND NOT \"QuantityTracked\""));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"MaterialCategories\" WHERE \"CoaRequired\""));
            Assert.Equal("SOL", await Text(c, "SELECT \"CodePrefix\" FROM \"MaterialCategories\" WHERE \"Name\" = 'Solvent'"));
            Assert.Equal("IND2", await Text(c, "SELECT \"CodePrefix\" FROM \"MaterialCategories\" WHERE \"Name\" = 'Indicator solution'"));
            // Sections: the four microbiology categories are micro-only, the rest are available everywhere.
            Assert.Equal(4, await Scalar(c, $"SELECT count(*) FROM \"MaterialCategorySections\" WHERE \"SectionId\" = {micro}"));
            Assert.Equal(4, await Scalar(c, "SELECT count(*) FROM \"MaterialCategorySections\""));

            // Entries: one per (category, normalised name) with a sequence per category ordered by name.
            Assert.Equal(9, await Scalar(c, "SELECT count(*) FROM \"MaterialMasterEntries\" WHERE \"Category\" = 4"));
            var sup1 = await EntryOf(c, s1);
            Assert.Equal(sup1, await EntryOf(c, s2));
            Assert.NotEqual(sup1, await EntryOf(c, s3));
            Assert.Equal("SUP-001", await Text(c, $"SELECT \"Code\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {sup1}"));
            Assert.Equal("Egg Yolk Tellurite", await Text(c, $"SELECT \"Name\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {sup1}"));
            Assert.Equal(micro, await Scalar(c, $"SELECT \"SectionId\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {sup1}"));
            Assert.Equal("SUP-002", await Text(c, $"SELECT \"Code\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {await EntryOf(c, s3)}"));
            var gloves = await EntryOf(c, d1);
            Assert.Equal(gloves, await EntryOf(c, d2));
            Assert.Equal(micro, await Scalar(c, $"SELECT \"SectionId\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {gloves}")); // earliest lot's lab
            Assert.Equal("GLOVES", await Text(c, $"SELECT \"Name\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {gloves}"));
            var solvent = await EntryOf(c, o1);
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"MaterialMasterEntries\" e JOIN \"MaterialCategories\" k ON k.\"Id\" = e.\"MaterialCategoryId\" WHERE e.\"Id\" = {solvent} AND k.\"Name\" = 'Solvent'"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"MaterialMasterEntries\" e JOIN \"MaterialCategories\" k ON k.\"Id\" = e.\"MaterialCategoryId\" WHERE e.\"Id\" = {await EntryOf(c, o5)} AND k.\"Name\" = 'General'"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"MaterialMasterEntries\" e JOIN \"MaterialCategories\" k ON k.\"Id\" = e.\"MaterialCategoryId\" WHERE e.\"Id\" = {await EntryOf(c, o6)} AND k.\"Name\" = 'General'"));
            Assert.StartsWith("IND2-", await Text(c, $"SELECT \"Code\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {await EntryOf(c, o4)}"));

            // Reagent lots link by code, then by a unique name; the ambiguous and unknown ones stay unlinked.
            Assert.Equal(rg1, await EntryOf(c, byCode));
            Assert.Equal(rg2, await EntryOf(c, byName));
            Assert.Equal(0, await Scalar(c, $"SELECT count(\"MaterialMasterEntryId\") FROM \"Materials\" WHERE \"Id\" IN ({ambiguous}, {unknown}, {media})"));

            // Snapshots are untouched.
            Assert.Equal("egg yolk   tellurite ", await Text(c, $"SELECT \"MaterialName\" FROM \"Materials\" WHERE \"Id\" = {s2}"));
            Assert.Equal(2, await Scalar(c, $"SELECT \"MaterialType\" FROM \"Materials\" WHERE \"Id\" = {s2}"));
            Assert.Equal(10, await Scalar(c, $"SELECT \"MaterialType\" FROM \"Materials\" WHERE \"Id\" = {o2}"));
            Assert.Equal("RG-001", await Text(c, $"SELECT \"Code\" FROM \"Materials\" WHERE \"Id\" = {byCode}"));

            // One audit row per category, per entry and per linked lot, plus the summary.
            Assert.Equal(9, await Audits(c, "MaterialCategory"));
            Assert.Equal(9, await Audits(c, "MaterialMasterEntry"));
            Assert.Equal(13, await Audits(c, "Material")); // 11 other-material lots + 2 reagent lots
            var summary = await Text(c, $"SELECT \"NewValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'MaterialBackfill'");
            Assert.Contains("\"ReagentAndStandardLotsUnmatchedCount\": 2", summary);
            Assert.Contains($"[{ambiguous}, {unknown}]", summary);
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"AuditLogs\" WHERE \"EntityName\" = 'Material' AND \"EntityId\" = '{s1}' AND \"Reason\" = 'Master Testing Materials backfill' AND \"ActorType\" = 2 AND \"NewValue\" = '{{\"MaterialMasterEntryId\" : {sup1}}}'"));

            // The new permissions exist, are enforced and sit with the built-in Section Head role only.
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" IN ('MaterialCategories.Manage', 'Materials.Release') AND \"IsEnforced\""));

            await migrator.MigrateAsync(Before);
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" IN ('MaterialCategories.Manage', 'Materials.Release')"));
        }
        finally
        {
            await ScratchPostgresDatabase.DropAsync(admin, name);
        }
    }

    private static Task<int> Lot(NpgsqlConnection c, int section, int type, string name, string received, string? code = null, string? customType = null, int unit = 0) =>
        Insert(c, "Materials", ("SectionId", section), ("MaterialType", type), ("MaterialName", name), ("Code", code!), ("CustomType", customType!),
            ("ReceivingDate", DateTime.SpecifyKind(DateTime.Parse(received), DateTimeKind.Utc)), ("Unit", unit));

    private static async Task<int> EntryOf(NpgsqlConnection c, int lotId) =>
        (int)await Scalar(c, $"SELECT \"MaterialMasterEntryId\" FROM \"Materials\" WHERE \"Id\" = {lotId}");

    private static Task<long> Audits(NpgsqlConnection c, string entity) =>
        Scalar(c, $"SELECT count(*) FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = '{entity}'");

    // Inserts one row; every NOT NULL column without a default gets a type-based filler unless overridden (null overrides are skipped).
    private static async Task<int> Insert(NpgsqlConnection c, string table, params (string Col, object? Val)[] over)
    {
        var values = over.Where(o => o.Val is not null).ToDictionary(o => o.Col, o => o.Val!);
        await using (var q = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @t AND is_nullable = 'NO' " +
            "AND column_default IS NULL AND is_identity = 'NO'", c))
        {
            q.Parameters.AddWithValue("t", table);
            await using var r = await q.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var col = r.GetString(0);
                if (values.ContainsKey(col)) continue;
                values[col] = r.GetString(1) switch
                {
                    "integer" or "bigint" or "smallint" => 1,
                    "numeric" => 1m,
                    "boolean" => false,
                    "timestamp with time zone" => DateTime.UtcNow,
                    "timestamp without time zone" => DateTime.UtcNow,
                    "date" => DateOnly.FromDateTime(DateTime.UtcNow),
                    "jsonb" or "json" => "{}",
                    _ => "x"
                };
            }
        }
        var cols = string.Join(",", values.Keys.Select(k => $"\"{k}\""));
        var ps = string.Join(",", values.Keys.Select((_, i) => $"@p{i}"));
        await using var cmd = new NpgsqlCommand($"INSERT INTO \"{table}\" ({cols}) VALUES ({ps}) RETURNING \"Id\"", c);
        var n = 0;
        foreach (var v in values.Values) cmd.Parameters.AddWithValue($"p{n++}", v);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> Scalar(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task<string> Text(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }
}
