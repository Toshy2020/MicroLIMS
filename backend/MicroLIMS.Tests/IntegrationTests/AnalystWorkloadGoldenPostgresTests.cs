using Microsoft.EntityFrameworkCore;
using MicroLIMS.Tests.Fixtures;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.IntegrationTests;

// Runs the S3 read model on real Postgres (translation of the delivered/open queries, DateOnly columns, check
// constraints) against a hand-calculated dataset. Own throw-away database, so no other test's rows interfere.
public class AnalystWorkloadGoldenPostgresTests
{
    [PostgresFact]
    public async Task Workload_MatchesTheHandCalculatedGoldenDataset()
    {
        await ScratchPostgresDatabase.RunAsync(async cs =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(cs);
            await db.Database.MigrateAsync();
            // AddOrgSectionSegregation already inserted QC and MICRO into a migrated database (PostgresTestFixture says the same);
            // SeedBaselineAsync inserts its own ids and codes, so clear the migration-seeded org rows first.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"MaterialCategorySections\"; DELETE FROM \"DocumentSections\"; DELETE FROM \"DocumentDepartments\";");
            await SeedBaselineAsync(db);

            await DeliverAsync(db, 1, "TAMC", Bob, At(9, 12), returned: true);
            await DeliverAsync(db, 2, "TAMC", Bob, At(9, 12));
            await DeliverAsync(db, 3, "PH", Bob, At(9, 12));
            await DeliverAsync(db, 4, "TAMC", Bob, At(11, 12), submittedAt: At(11, 10));
            await DeliverAsync(db, 5, "TAMC", Bob, At(11, 12), submittedAt: At(11, 10));
            await DeliverAsync(db, 6, "NOPROF", Bob, At(9, 12));
            await DeliverAsync(db, 7, "TAMC", Bob, At(9, 12), superseded: true);
            await OpenAsync(db, 500, "PH", Bob);

            var service = TestServiceFactory.AnalystWorkload(db, Clock());
            var dto = await service.GetAsync(Week(analystId: Bob), default);

            var row = Assert.Single(dto.Rows);
            Assert.Equal(Bob, row.UserId);
            Assert.Equal(30m, row.CapacityUnits);
            Assert.Equal(12m, row.DeliveredUnits);
            Assert.Equal(6, row.DeliveredTests);
            Assert.Equal(0.4m, row.Utilisation);
            Assert.False(row.UtilisationCheckProfile);
            Assert.Equal(0.6667m, Math.Round(row.WeightedOnTime!.Value, 4));
            Assert.Equal(0.8333m, Math.Round(row.WeightedRightFirstTime!.Value, 4));
            Assert.Equal(4m, row.OpenUnits);
            Assert.Equal(30m, row.ForwardCapacityUnits);
            Assert.Equal(0.1333m, Math.Round(row.ForwardLoad!.Value, 4));
            Assert.False(row.ForwardLoadOverCapacity);
            Assert.Equal(1, row.UnweightedTests);
            Assert.Equal(1, row.TestsOnDefaultTarget);
            Assert.True(dto.CapacityUsesDefaults);
            Assert.Equal(1, dto.Gaps.Single(g => g.Code == "UNWEIGHTED_TESTS").Count);

            var page = await service.GetTestsAsync(Week(analystId: Bob), 1, 50, default);
            Assert.Equal(7, page.Total); // 6 delivered + 1 open; the superseded test is not listed
        });
    }
}
