using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;
using static MicroLIMS.Tests.IntegrationTests.RetireCalibrationRunsMigrationTests;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the step BEFORE the ICP redesign, seed the old method-based data,
// then migrate on and check the old ICP data is gone, nothing else is touched and the new schema is in place.
public class IcpTestLevelElementsMigrationTests
{
    private const string Before = "20261105075244_PreparationMaterialTraceability";
    private const string Target = "20261105134043_IcpTestLevelElements";
    private const int IcpWorkflowType = 14;

    [PostgresFact]
    public async Task Migration_RemovesOldIcpData_KeepsEverythingElse_AndCreatesTheNewSchema()
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
            // Parents are not needed for this test: drop the FKs while seeding, re-add them NOT VALID below.
            var fks = new List<(string Table, string Name, string Def)>();
            await using (var q = new NpgsqlCommand(
                "SELECT conrelid::regclass::text, conname, pg_get_constraintdef(oid) FROM pg_constraint " +
                "WHERE contype = 'f' AND connamespace = 'public'::regnamespace", c))
            await using (var r = await q.ExecuteReaderAsync())
                while (await r.ReadAsync()) fks.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} DROP CONSTRAINT \"{fk.Name}\"");

            // (a) the old ICP test, its order chain, an item specification keyed by an old method element, and a run.
            await Insert(c, "TestDefinitions", ("Code", "ICP-OLD"), ("WorkflowType", IcpWorkflowType), ("EquationType", 1), ("IcpMethodId", 7));
            var (icpOrder, icpAnalysis, icpResult) = await OrderChain(c, "ICP-OLD");
            await Insert(c, "SampleTests", ("TestCode", "ICP-OLD"));
            await Insert(c, "Specifications", ("TestCode", "ICP-OLD"), ("IcpMethodElementId", 3));
            var run = await Insert(c, "IcpRuns", ("IcpMethodId", 7));
            var runSample = await Insert(c, "IcpRunSamples", ("IcpRunId", run), ("TestOrderId", icpOrder));
            await Insert(c, "IcpEvidences", ("IcpRunId", run), ("IcpRunSampleId", runSample));
            var calibration = await Insert(c, "IcpCalibrations", ("IcpRunId", run));
            await Insert(c, "IcpCalibrationElements", ("IcpCalibrationId", calibration), ("IcpMethodElementId", 3));

            // (b) an unrelated count test (must survive untouched)
            await Insert(c, "TestDefinitions", ("Code", "COUNT-1"), ("WorkflowType", 1));
            var (countOrder, countAnalysis, countResult) = await OrderChain(c, "COUNT-1");
            await Insert(c, "Specifications", ("TestCode", "COUNT-1"));

            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} ADD CONSTRAINT \"{fk.Name}\" {fk.Def} NOT VALID");

            await migrator.MigrateAsync(Target);

            // Old ICP data is gone.
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"IcpRuns\""));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"IcpRunSamples\""));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"IcpEvidences\""));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"IcpCalibrations\""));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"IcpCalibrationElements\""));
            Assert.Equal(0, await Scalar(c, $"SELECT count(*) FROM \"TestOrders\" WHERE \"Id\" = {icpOrder}"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(*) FROM \"TestAnalyses\" WHERE \"Id\" = {icpAnalysis}"));
            Assert.Equal(0, await Scalar(c, $"SELECT count(*) FROM \"ParameterResults\" WHERE \"Id\" = {icpResult}"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"ResultRecords\" WHERE \"TestCode\" = 'ICP-OLD'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"SampleTests\" WHERE \"TestCode\" = 'ICP-OLD'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"Specifications\" WHERE \"TestCode\" = 'ICP-OLD'"));

            // The ICP test itself stays, unconfigured: its old method id must not be read as the new sample form.
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"TestDefinitions\" WHERE \"Code\" = 'ICP-OLD'"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"TestDefinitions\" WHERE \"Code\" = 'ICP-OLD' AND \"IcpMode\" IS NULL AND \"IcpSampleForm\" IS NULL"));

            // Everything else is untouched.
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"TestDefinitions\" WHERE \"Code\" = 'COUNT-1' AND \"WorkflowType\" = 1"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"TestOrders\" WHERE \"Id\" = {countOrder}"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"TestAnalyses\" WHERE \"Id\" = {countAnalysis}"));
            Assert.Equal(1, await Scalar(c, $"SELECT count(*) FROM \"ParameterResults\" WHERE \"Id\" = {countResult}"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"Specifications\" WHERE \"TestCode\" = 'COUNT-1'"));

            // New schema.
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'TestDefinitionIcpElements'"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'IcpSampleConcentrations'"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'Specifications' AND column_name = 'IcpTestElementId'"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'IcpRunSamples' AND column_name = 'ActualWeightG'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.tables WHERE table_name IN ('IcpMethods', 'IcpMethodElements', 'IcpCcvReadings', 'IcpSampleReplicates', 'IcpReplicateConcentrations')"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'Specifications' AND column_name = 'IcpMethodElementId'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'IcpRuns' AND column_name IN ('IcpMethodId', 'MethodSnapshotJson')"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }
}
