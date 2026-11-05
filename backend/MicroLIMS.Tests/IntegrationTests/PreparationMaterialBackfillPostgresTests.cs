using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE PreparationMaterialTraceability, seed Item preparation
// configurations with free-text diluent / neutralizer covering every backfill branch, migrate on, and check the
// mapped sources, the untouched text and approval, the audited run and the dropped DiluentTypes table.
public class PreparationMaterialBackfillPostgresTests
{
    private const string Before = "20261105072354_MediaAdditives";
    private const string Target = "20261105075244_PreparationMaterialTraceability";
    private const string Process = "Migration " + Target;

    // Source ints: 0 MediaProduct, 1 SolutionRecipe, 2 MaterialEntry, 3 ContainedInMedium.
    [PostgresFact]
    public async Task Migration_MapsUniqueMatches_LeavesTextAndApproval_ReportsTheRest_AndAuditsEverything()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(cs);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            var micro = (int)await Scalar(c, "SELECT \"Id\" FROM \"DocumentSections\" WHERE \"Code\" = 'MICRO'");
            var otherSection = (int)await Scalar(c, "SELECT \"Id\" FROM \"DocumentSections\" WHERE \"Code\" <> 'MICRO' ORDER BY \"Id\" LIMIT 1");

            var tsb = await Insert(c, "MediaProducts", ("Name", "TSB"), ("Code", "TSB"), ("IsActive", true));
            var bpw = await Insert(c, "MediaProducts", ("Name", "Buffered peptone water"), ("Code", "BPW"), ("IsActive", true));
            var oldBroth = await Insert(c, "MediaProducts", ("Name", "Old broth"), ("Code", "OLD"), ("IsActive", false));

            // Solution types: 0 MobilePhase, 1 Diluent, 2 Titrant, 3 Reagent, 4 Neutralizer.
            var buffer = await Insert(c, "SolutionMasters", ("SectionId", micro), ("Name", "Phosphate buffer"), ("Type", 1), ("IsActive", true));
            var letheen = await Insert(c, "SolutionMasters", ("SectionId", micro), ("Name", "Letheen neutralizer"), ("Type", 4), ("IsActive", true));
            await Insert(c, "SolutionMasters", ("SectionId", micro), ("Name", "Mobile X"), ("Type", 0), ("IsActive", true));
            await Insert(c, "SolutionMasters", ("SectionId", micro), ("Name", "Saline"), ("Type", 1), ("IsActive", true));
            await Insert(c, "SolutionMasters", ("SectionId", otherSection), ("Name", "Saline"), ("Type", 1), ("IsActive", true));
            var peptone = await Insert(c, "MaterialMasterEntries", ("SectionId", micro), ("Code", "PW-1"), ("Name", "Peptone water"), ("IsActive", true));

            var items = new List<int>();
            for (var i = 0; i < 6; i++) items.Add(await Insert(c, "Items", ("Name", "Item " + i), ("Code", "IT-" + i)));

            // 1: case/whitespace-insensitive media name and the "none" neutralizer; approved, must stay approved.
            var cfg1 = await Config(c, items[0], "  tsb ", "None", approval: 1);
            // 2: a media CODE, and a general solution recipe.
            var cfg2 = await Config(c, items[1], "bpw", "LETHEEN NEUTRALIZER", approval: 0);
            // 3: a recipe of the diluent type; the neutralizer is contained in the medium.
            var cfg3 = await Config(c, items[2], "Phosphate buffer", "Contained in the medium", approval: 1);
            // 4: a HPLC mobile phase is not a general recipe (no match); "n/a" neutralizer.
            var cfg4 = await Config(c, items[3], "Mobile X", "n/a", approval: 1);
            // 5: the same name in two sections is ambiguous; a material entry matched by code.
            var cfg5 = await Config(c, items[4], "Saline", "pw-1", approval: 1);
            // 6: an inactive product and an unknown text match nothing.
            var cfg6 = await Config(c, items[5], "Old broth", "Mystery", approval: 1);
            await Insert(c, "DiluentTypes", ("Name", "Buffer"), ("RequiresBatchTracking", false));

            await migrator.MigrateAsync(Target);

            async Task<long?> Value(int config, string column) => await NullableScalar(c, $"SELECT \"{column}\" FROM \"ItemPreparationConfigurations\" WHERE \"Id\" = {config}");

            Assert.Equal(0, await Value(cfg1, "DiluentSource"));
            Assert.Equal(tsb, await Value(cfg1, "DiluentMediaProductId"));
            Assert.Equal(3, await Value(cfg1, "NeutralizerSource"));
            Assert.Null(await Value(cfg1, "NeutralizerMediaProductId"));
            Assert.Equal(0, await Value(cfg2, "DiluentSource"));
            Assert.Equal(bpw, await Value(cfg2, "DiluentMediaProductId"));
            Assert.Equal(1, await Value(cfg2, "NeutralizerSource"));
            Assert.Equal(letheen, await Value(cfg2, "NeutralizerSolutionMasterId"));
            Assert.Equal(1, await Value(cfg3, "DiluentSource"));
            Assert.Equal(buffer, await Value(cfg3, "DiluentSolutionMasterId"));
            Assert.Equal(3, await Value(cfg3, "NeutralizerSource"));
            Assert.Null(await Value(cfg4, "DiluentSource"));
            Assert.Equal(3, await Value(cfg4, "NeutralizerSource"));
            Assert.Null(await Value(cfg5, "DiluentSource"));
            Assert.Equal(2, await Value(cfg5, "NeutralizerSource"));
            Assert.Equal(peptone, await Value(cfg5, "NeutralizerMaterialMasterEntryId"));
            Assert.Null(await Value(cfg6, "DiluentSource"));
            Assert.Null(await Value(cfg6, "NeutralizerSource"));

            // The text and the approval are never touched.
            Assert.Equal("  tsb ", await Text(c, $"SELECT \"Diluent\" FROM \"ItemPreparationConfigurations\" WHERE \"Id\" = {cfg1}"));
            Assert.Equal("None", await Text(c, $"SELECT \"Neutralizer\" FROM \"ItemPreparationConfigurations\" WHERE \"Id\" = {cfg1}"));
            Assert.Equal(1, await Value(cfg1, "ApprovalStatus"));
            Assert.Equal(0, await Value(cfg2, "ApprovalStatus"));

            // One audit row per configuration that got a mapping (6 has none), plus the summary.
            Assert.Equal(5, await Audits(c, "ItemPreparationConfiguration"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(*) FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'ItemPreparationConfiguration' AND \"EntityId\" = '{cfg6}'"));
            var audit = await Text(c, $"SELECT \"NewValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'ItemPreparationConfiguration' AND \"EntityId\" = '{cfg2}'");
            Assert.Contains("\"DiluentSource\": \"MediaProduct\"", audit);
            Assert.Contains("\"NeutralizerSource\": \"SolutionRecipe\"", audit);
            Assert.Equal("Diluent/neutralizer traceability backfill", await Text(c, $"SELECT \"Reason\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'ItemPreparationConfiguration' AND \"EntityId\" = '{cfg2}'"));

            var summary = await Text(c, $"SELECT \"NewValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'ItemPreparationConfigurationBackfill'");
            Assert.Contains("\"ConfigurationsMapped\": 5", summary);
            Assert.Contains("\"MappedCount\": 8", summary);
            Assert.Contains("\"UnmappedCount\": 4", summary);
            Assert.Contains("\"ContainedInMedium\": 3", summary);
            Assert.Contains("\"MediaProduct\": 2", summary);
            Assert.Contains("AmbiguousMatch", summary);
            Assert.Contains("NoMatch", summary);
            Assert.Contains($"\"ConfigurationId\": {cfg5}", summary);

            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'DiluentTypes'"));
        });
    }

    private static Task<int> Config(NpgsqlConnection c, int item, string diluent, string neutralizer, int approval) =>
        Insert(c, "ItemPreparationConfigurations", ("ItemId", item), ("Amount", 10m), ("Technique", "PourPlate"),
            ("Diluent", diluent), ("Neutralizer", neutralizer), ("ApprovalStatus", approval));

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

    private static async Task<long?> NullableScalar(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? null : Convert.ToInt64(v);
    }

    private static async Task<string> Text(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
}
