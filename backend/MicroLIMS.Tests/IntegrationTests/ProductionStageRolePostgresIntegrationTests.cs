using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres round trip for FP Standard-Comparison Assay Slice 1: the
// AddProductionStageRole migration's data backfill (real SQL, not EF), the
// Sample.ProductionStageId FK's ON DELETE SET NULL behavior (a real
// database constraint, not just EF's client-side cascade), and the
// TestDefinitionStageReplicates table's unique index and cascade delete.
[Collection("PostgresDatabaseCollection")]
public class ProductionStageRolePostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public ProductionStageRolePostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task Migration_MapsSeededProductionStageNamesToRoles_AndInsertsStabilityRole()
    {
        // The fixture applies every EF migration (including AddProductionStageRole)
        // at InitializeAsync, so the seeded rows from AddSamplersAndProductionStages
        // are already role-classified by the time this test runs.
        await using var db = _fixture.CreateDbContext();

        var byName = await db.ProductionStages.AsNoTracking().ToDictionaryAsync(s => s.Name, s => s.Role);

        Assert.Equal(ProductionStageRole.Bulk, byName["B"]);
        Assert.Equal(ProductionStageRole.InProcess, byName["IP"]);
        Assert.Equal(ProductionStageRole.Finished, byName["F.P"]);
        Assert.Equal(ProductionStageRole.Finished, byName["S.F"]);
        Assert.Equal(ProductionStageRole.InProcess, byName["Coating"]);
        Assert.Equal(ProductionStageRole.InProcess, byName["Compressed Tab"]);

        Assert.True(byName.ContainsKey("Stability"));
        Assert.Equal(ProductionStageRole.Stability, byName["Stability"]);
    }

    [PostgresFact]
    public async Task Migration_StabilityInsertIsIdempotent_ReRunningDoesNotDuplicate()
    {
        await using var db = _fixture.CreateDbContext();

        var before = await db.ProductionStages.CountAsync(s => s.Name == "Stability");
        Assert.Equal(1, before);

        // Re-run the exact idempotent SQL the migration uses.
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ""ProductionStages"" (""Name"", ""IsActive"", ""Role"")
            SELECT 'Stability', true, 4
            WHERE NOT EXISTS (SELECT 1 FROM ""ProductionStages"" WHERE ""Name"" = 'Stability');
        ");

        var after = await db.ProductionStages.CountAsync(s => s.Name == "Stability");
        Assert.Equal(1, after);
    }

    [PostgresFact]
    public async Task UnrecognisedName_NeverTouchedByTheMigration_DefaultsToOtherRole()
    {
        await using var db = _fixture.CreateDbContext();

        // Mirrors exactly what the migration guarantees for any row it does not
        // recognize: the Role column's own default (0 = Other) applies, since the
        // migration's UPDATE statements only ever match the six known literal names.
        var stage = new ProductionStage { Name = "Custom Blend " + Guid.NewGuid().ToString("N")[..6] };
        db.ProductionStages.Add(stage);
        await db.SaveChangesAsync();

        await using var verify = _fixture.CreateDbContext();
        var reloaded = await verify.ProductionStages.AsNoTracking().FirstAsync(s => s.Id == stage.Id);
        Assert.Equal(ProductionStageRole.Other, reloaded.Role);
    }

    [PostgresFact]
    public async Task SampleProductionStageId_RoundTrips_AndIsSetNullWhenStageDeleted()
    {
        await using var db = _fixture.CreateDbContext();

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? (await db.CausesOfTesting.AddAsync(new CauseOfTesting { Name = "Routine", IsActive = true })).Entity;
        await db.SaveChangesAsync();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var stage = new ProductionStage { Name = $"Finished-{suffix}", Role = ProductionStageRole.Finished };
        db.ProductionStages.Add(stage);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = $"FP-{suffix}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = cause.Id,
            ControlNumber = "CTRL-1",
            SampledBy = "Analyst",
            ReceivedByUserId = _fixture.SeededUserId,
            ProductionStage = stage.Name,
            ProductionStageId = stage.Id
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        // Confirm the FK round-tripped through a fresh context first.
        await using (var verify = _fixture.CreateDbContext())
        {
            var reloaded = await verify.Samples.AsNoTracking().FirstAsync(s => s.Id == sample.Id);
            Assert.Equal(stage.Id, reloaded.ProductionStageId);
        }

        // Delete the stage directly via SQL, on a context that never loaded the
        // Sample - this exercises Postgres's own ON DELETE SET NULL constraint,
        // not EF's client-side cascade fixup.
        await using (var deleteDb = _fixture.CreateDbContext())
        {
            await deleteDb.Database.ExecuteSqlInterpolatedAsync($@"DELETE FROM ""ProductionStages"" WHERE ""Id"" = {stage.Id}");
        }

        await using var final = _fixture.CreateDbContext();
        var afterDelete = await final.Samples.AsNoTracking().FirstAsync(s => s.Id == sample.Id);
        Assert.Null(afterDelete.ProductionStageId);
        // The historical string snapshot survives the stage's deletion untouched.
        Assert.Equal(stage.Name, afterDelete.ProductionStage);
    }

    [PostgresFact]
    public async Task TestDefinitionStageReplicates_UniqueIndex_RejectsDuplicateRoleForSameTest()
    {
        await using var db = _fixture.CreateDbContext();
        var testDef = new TestDefinition { Code = "SCA-" + Guid.NewGuid().ToString("N")[..8], DisplayName = "SCA Test", SectionId = _fixture.SeededSectionId, IsActive = true };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Bulk,
            StandardReplicates = 6,
            SampleReplicates = 1
        });
        await db.SaveChangesAsync();

        await using var duplicate = _fixture.CreateDbContext();
        duplicate.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Bulk,
            StandardReplicates = 3,
            SampleReplicates = 2
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task TestDefinitionStageReplicates_CascadeDeletesWithTestDefinition()
    {
        await using var db = _fixture.CreateDbContext();
        var testDef = new TestDefinition { Code = "SCA-CASC-" + Guid.NewGuid().ToString("N")[..8], DisplayName = "SCA Cascade Test", SectionId = _fixture.SeededSectionId, IsActive = true };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Finished,
            StandardReplicates = 6,
            SampleReplicates = 2
        });
        await db.SaveChangesAsync();

        db.TestDefinitions.Remove(testDef);
        await db.SaveChangesAsync();

        await using var verify = _fixture.CreateDbContext();
        Assert.False(await verify.TestDefinitionStageReplicates.AnyAsync(r => r.TestDefinitionId == testDef.Id));
    }
}
