using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE StepMediaDiluentMediaProduct, seed old-schema step media and
// diluent types covering every backfill branch, migrate on, and check the audited run (links, unresolved rows, summary).
public class StepMediaDiluentBackfillPostgresTests
{
    private const string Before = "20261028123607_ReferenceOrganisms";
    private const string Target = "20261028130815_StepMediaDiluentMediaProduct";
    private const string Process = "Migration " + Target;

    [PostgresFact]
    public async Task Migration_LinksStepMediaAndDiluentTypesToProducts_ReportsTheRest_AndAuditsEverything()
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

            var tsb = await Insert(c, "MediaProducts", ("Name", "TSB"), ("Code", "TSB"));
            var xld = await Insert(c, "MediaProducts", ("Name", "XLD"), ("Code", "XLD"));
            var condTsb = await Insert(c, "MediaIncubationConditions", ("MediaProductId", tsb));
            var condXld = await Insert(c, "MediaIncubationConditions", ("MediaProductId", xld));

            var lotTsb = await Lot(c, micro, "L1", tsb);
            var lotTsbSecond = await Lot(c, micro, "L2", tsb);
            var lotXld = await Lot(c, micro, "L3", xld);
            var lotNoProduct = await Lot(c, micro, "L4", null);

            var test = await Insert(c, "TestDefinitions", ("Code", "T1"));
            var s1 = await Insert(c, "TestWorkflowSteps", ("TestDefinitionId", test), ("StepOrder", 1));
            var s2 = await Insert(c, "TestWorkflowSteps", ("TestDefinitionId", test), ("StepOrder", 2));
            var s3 = await Insert(c, "TestWorkflowSteps", ("TestDefinitionId", test), ("StepOrder", 3));

            // Condition wins even when the lot has no product; the lot is the fallback without a condition.
            var viaCondition = await StepMedium(c, s1, lotNoProduct, condTsb);
            var viaLot = await StepMedium(c, s1, lotXld, null);
            // The condition's product overrides a lot of another product (the rule StepMediumMatcher used to apply).
            var conditionOverrides = await StepMedium(c, s2, lotTsb, condXld);
            // Neither side has a product: nothing is invented.
            var noProduct = await StepMedium(c, s2, lotNoProduct, null);
            // Two rows of the same product on one step: the lowest id keeps it, the other is reported.
            var firstOfProduct = await StepMedium(c, s3, lotTsb, null);
            var duplicateOfProduct = await StepMedium(c, s3, lotTsbSecond, null);

            var dTracked = await Diluent(c, "TSB", true, lotTsb);
            var dPlain = await Diluent(c, "Buffer", false, null);
            var dLotWithoutProduct = await Diluent(c, "Broken A", true, lotNoProduct);
            var dTrackedWithoutLot = await Diluent(c, "Broken B", true, null);

            await migrator.MigrateAsync(Target);

            Assert.Equal(tsb, await Scalar(c, $"SELECT \"MediaProductId\" FROM \"TestWorkflowStepMedias\" WHERE \"Id\" = {viaCondition}"));
            Assert.Equal(xld, await Scalar(c, $"SELECT \"MediaProductId\" FROM \"TestWorkflowStepMedias\" WHERE \"Id\" = {viaLot}"));
            Assert.Equal(xld, await Scalar(c, $"SELECT \"MediaProductId\" FROM \"TestWorkflowStepMedias\" WHERE \"Id\" = {conditionOverrides}"));
            Assert.Equal(tsb, await Scalar(c, $"SELECT \"MediaProductId\" FROM \"TestWorkflowStepMedias\" WHERE \"Id\" = {firstOfProduct}"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(\"MediaProductId\") FROM \"TestWorkflowStepMedias\" WHERE \"Id\" IN ({noProduct}, {duplicateOfProduct})"));

            // The legacy stock lot is kept for history.
            Assert.Equal(lotNoProduct, await Scalar(c, $"SELECT \"MaterialId\" FROM \"TestWorkflowStepMedias\" WHERE \"Id\" = {viaCondition}"));
            Assert.Equal(lotTsbSecond, await Scalar(c, $"SELECT \"MaterialId\" FROM \"TestWorkflowStepMedias\" WHERE \"Id\" = {duplicateOfProduct}"));

            Assert.Equal(tsb, await Scalar(c, $"SELECT \"MediaProductId\" FROM \"DiluentTypes\" WHERE \"Id\" = {dTracked}"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(\"MediaProductId\") FROM \"DiluentTypes\" WHERE \"Id\" IN ({dPlain}, {dLotWithoutProduct}, {dTrackedWithoutLot})"));

            // One audit row per linked row, plus the summary naming every unresolved row.
            Assert.Equal(4, await Audits(c, "TestWorkflowStepMedia"));
            Assert.Equal(1, await Audits(c, "DiluentType"));
            var previous = await Text(c, $"SELECT \"PreviousValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'DiluentType'");
            Assert.Contains($"\"MaterialId\" : {lotTsb}", previous);
            var summary = await Text(c, $"SELECT \"NewValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'StepMediaDiluentBackfill'");
            Assert.Contains("\"StepMediaLinkedFromCondition\": 2", summary);
            Assert.Contains("\"StepMediaLinkedFromMaterial\": 2", summary);
            Assert.Contains("\"StepMediaConditionOverrodeMaterial\": 1", summary);
            Assert.Contains("\"StepMediaUnresolvedCount\": 2", summary);
            Assert.Contains($"\"StepMediaId\": {noProduct}", summary);
            Assert.Contains("NoProductOnConditionOrMaterial", summary);
            Assert.Contains($"\"StepMediaId\": {duplicateOfProduct}", summary);
            Assert.Contains("DuplicateProductOnStep", summary);
            Assert.Contains("\"DiluentTypesLinked\": 1", summary);
            Assert.Contains("\"DiluentTypesUnresolvedCount\": 2", summary);
            Assert.Contains($"\"DiluentTypeId\": {dLotWithoutProduct}", summary);
            Assert.Contains($"\"DiluentTypeId\": {dTrackedWithoutLot}", summary);

            // The old DiluentTypes.MaterialId is gone; the new unique index allows one row per product on a step.
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'DiluentTypes' AND column_name = 'MaterialId'"));
            await Assert.ThrowsAsync<PostgresException>(() => StepMediumWithProduct(c, s1, tsb));
        }
        finally
        {
            await ScratchPostgresDatabase.DropAsync(admin, name);
        }
    }

    private static Task<int> Lot(NpgsqlConnection c, int section, string batch, int? product) =>
        Insert(c, "Materials", ("SectionId", section), ("MaterialType", 0), ("MaterialName", "Lot " + batch), ("BatchNumber", batch),
            ("Code", "CODE-" + batch), ("MediaProductId", product), ("Unit", 0));

    private static Task<int> StepMedium(NpgsqlConnection c, int step, int material, int? condition) =>
        Insert(c, "TestWorkflowStepMedias", ("TestWorkflowStepId", step), ("MaterialId", material), ("MediaIncubationConditionId", condition));

    private static Task<int> StepMediumWithProduct(NpgsqlConnection c, int step, int product) =>
        Insert(c, "TestWorkflowStepMedias", ("TestWorkflowStepId", step), ("MediaProductId", product));

    private static Task<int> Diluent(NpgsqlConnection c, string name, bool tracked, int? material) =>
        Insert(c, "DiluentTypes", ("Name", name), ("RequiresBatchTracking", tracked), ("MaterialId", material));

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
