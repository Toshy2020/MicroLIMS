using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database (not the shared fixture one): the test must stop at
// the migration BEFORE RetireCalibrationRuns, seed rows in the old schema, then migrate on.
public class RetireCalibrationRunsMigrationTests
{
    private const string Before = "20261004204013_AddIcpRuns";
    private const string Target = "20261005064411_RetireCalibrationRuns";

    [PostgresFact]
    public async Task Migration_RepairsMisSavedHplcTest_AndKeepsItsData()
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
            // Parents are not needed for this test; skip FK checks on this connection only.
            await Exec(c, "SET session_replication_role = replica");

            // (a) HPLC-FOLIC shape: Workflow 3, Equation 13, HplcMethodId set
            await Insert(c, "TestDefinitions", ("Code", "HPLC-X"), ("WorkflowType", 3), ("EquationType", 13), ("HplcMethodId", 5));
            var (hplcOrder, hplcAnalysis, hplcResult) = await OrderChain(c, "HPLC-X");
            // (b) elemental: Workflow 3, CalibrationCurve
            var elDef = await Insert(c, "TestDefinitions", ("Code", "ICP-OLD"), ("WorkflowType", 3), ("EquationType", 3));
            var analyte = await Insert(c, "TestAnalytes", ("TestDefinitionId", elDef), ("Element", "Fe"));
            await Insert(c, "SampleTests", ("TestCode", "ICP-OLD"));
            await Insert(c, "Specifications", ("TestCode", "ICP-OLD"), ("TestAnalyteId", analyte));
            var (elOrder, _, elResult) = await OrderChain(c, "ICP-OLD");
            var run = await Insert(c, "CalibrationRuns", ("TestDefinitionId", elDef));
            var runAnalyte = await Insert(c, "CalibrationRunAnalytes", ("CalibrationRunId", run), ("TestAnalyteId", analyte));
            await Exec(c, $"UPDATE \"ParameterResults\" SET \"ValidityRecordItemId\" = {runAnalyte} WHERE \"Id\" = {elResult}");
            // (c) unrelated count test
            await Insert(c, "TestDefinitions", ("Code", "COUNT-1"), ("WorkflowType", 1));
            var (countOrder, _, _) = await OrderChain(c, "COUNT-1");

            await migrator.MigrateAsync(Target);

            Assert.Equal(12, await Scalar(c, "SELECT \"WorkflowType\" FROM \"TestDefinitions\" WHERE \"Code\" = 'HPLC-X'"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"TestOrders\" WHERE \"Id\" = {hplcOrder}"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"TestAnalyses\" WHERE \"Id\" = {hplcAnalysis}"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"ParameterResults\" WHERE \"Id\" = {hplcResult}"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"ResultRecords\" WHERE \"TestCode\" = 'HPLC-X'"));

            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"TestDefinitions\" WHERE \"Code\" = 'ICP-OLD'"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(*) FROM \"TestAnalytes\" WHERE \"Id\" = {analyte}"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"SampleTests\" WHERE \"TestCode\" = 'ICP-OLD'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"Specifications\" WHERE \"TestCode\" = 'ICP-OLD'"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(*) FROM \"TestOrders\" WHERE \"Id\" = {elOrder}"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(*) FROM \"ParameterResults\" WHERE \"Id\" = {elResult}"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"ResultRecords\" WHERE \"TestCode\" = 'ICP-OLD'"));

            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"TestDefinitions\" WHERE \"Code\" = 'COUNT-1' AND \"WorkflowType\" = 1"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"TestOrders\" WHERE \"Id\" = {countOrder}"));

            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.tables WHERE table_name LIKE 'CalibrationRun%'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'ParameterResults' AND column_name = 'ValidityRecordItemId'"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static async Task<(int order, int analysis, int result)> OrderChain(NpgsqlConnection c, string code)
    {
        var order = await Insert(c, "TestOrders", ("TestCode", code));
        var analysis = await Insert(c, "TestAnalyses", ("TestOrderId", order));
        var spec = await Insert(c, "Specifications", ("TestCode", code + "-RES"));
        var result = await Insert(c, "ParameterResults", ("TestOrderId", order), ("TestAnalysisId", analysis), ("SpecificationId", spec));
        await Insert(c, "ResultRecords", ("TestOrderId", order), ("TestCode", code), ("SourceId", order));
        return (order, analysis, result);
    }

    // Inserts one row; every NOT NULL column without a default gets a type-based filler unless overridden.
    private static async Task<int> Insert(NpgsqlConnection c, string table, params (string Col, object Val)[] over)
    {
        var values = over.ToDictionary(o => o.Col, o => o.Val);
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

    private static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await Exec(c, sql);
    }

    private static async Task Exec(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }
}
