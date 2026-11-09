using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Test Master "CoA order" (TestDefinition.CoaDisplayOrder) on PostgreSQL: it is
// configuration, so each change must persist through the migrated column and
// leave an attributable audit row (Rule 4.4). Arrange, Act and Assert use
// separate contexts and the assertions read with AsNoTracking (Rule 4.1).
[Collection("PostgresDatabaseCollection")]
public class TestDefinitionCoaOrderPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public TestDefinitionCoaOrderPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task CoaOrder_IsCreatedChangedAndCleared_WithAudit_AndOutOfRangeIsRejected()
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var userId = _fixture.SeededUserId;
        int sectionId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            sectionId = TestServiceFactory.EnsureMicroSection(arrangeDb).Id;
            TestServiceFactory.AssignUserToMicroSection(arrangeDb, userId);
            await arrangeDb.SaveChangesAsync();
        }

        // Create with an order.
        int id;
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var created = await TestServiceFactory.TestDefinitionMaster(actDb).CreateTestDefinitionAsync(userId,
                new CreateTestDefinitionRequest(Code: "ECOLI-" + tag, DisplayName: "Escherichia coli " + tag, SectionId: sectionId,
                    WorkflowType: WorkflowType.Observation) { CoaDisplayOrder = 3 });
            id = created.Id;
            Assert.Equal(3, created.CoaDisplayOrder);
        }
        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(3, (await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == id)).CoaDisplayOrder);
            var createAudit = await assertDb.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.EntityName == nameof(TestDefinition) && a.EntityId == id.ToString() && a.Action == "Create");
            Assert.Equal(userId, createAudit.UserId);
            Assert.Contains("\"CoaDisplayOrder\":3", createAudit.NewValue);
        }

        // Change it. An update that does not mention it keeps it.
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            var svc = TestServiceFactory.TestDefinitionMaster(actDb);
            await svc.UpdateTestDefinitionAsync(userId, id,
                new UpdateTestDefinitionRequest("ECOLI-" + tag, "Escherichia coli " + tag) { CoaDisplayOrder = 1 });
        }
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            await TestServiceFactory.TestDefinitionMaster(actDb).UpdateTestDefinitionAsync(userId, id,
                new UpdateTestDefinitionRequest("ECOLI-" + tag, "E. coli " + tag));
        }
        await using (var assertDb = _fixture.CreateDbContext())
        {
            var def = await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == id);
            Assert.Equal(1, def.CoaDisplayOrder);
            Assert.Equal("E. coli " + tag, def.DisplayName);

            var changeAudit = (await assertDb.AuditLogs.AsNoTracking()
                    .Where(a => a.EntityName == nameof(TestDefinition) && a.EntityId == id.ToString() && a.Action == "Update")
                    .ToListAsync())
                .Single(a => a.PreviousValue!.Contains("\"CoaDisplayOrder\":3"));
            Assert.Equal(userId, changeAudit.UserId);
            Assert.Contains("\"CoaDisplayOrder\":1", changeAudit.NewValue);
        }

        // Out of range is refused and changes nothing.
        foreach (var bad in new[] { -1, 10000 })
        {
            await using var actDb = _fixture.CreateDbContext();
            actDb.CurrentUserId = userId;
            await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.TestDefinitionMaster(actDb)
                .UpdateTestDefinitionAsync(userId, id, new UpdateTestDefinitionRequest("ECOLI-" + tag, "E. coli " + tag) { CoaDisplayOrder = bad }));
        }
        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.TestDefinitionMaster(actDb).CreateTestDefinitionAsync(userId,
                new CreateTestDefinitionRequest(Code: "BAD-" + tag, DisplayName: "Bad " + tag, SectionId: sectionId,
                    WorkflowType: WorkflowType.Observation) { CoaDisplayOrder = 0 }));
        }
        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(1, (await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == id)).CoaDisplayOrder);
            Assert.False(await assertDb.TestDefinitions.AsNoTracking().AnyAsync(t => t.Code == "BAD-" + tag));
        }

        // 0 clears it.
        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = userId;
            await TestServiceFactory.TestDefinitionMaster(actDb).UpdateTestDefinitionAsync(userId, id,
                new UpdateTestDefinitionRequest("ECOLI-" + tag, "E. coli " + tag) { CoaDisplayOrder = 0 });
        }
        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Null((await assertDb.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == id)).CoaDisplayOrder);
            var clearAudit = (await assertDb.AuditLogs.AsNoTracking()
                    .Where(a => a.EntityName == nameof(TestDefinition) && a.EntityId == id.ToString() && a.Action == "Update")
                    .ToListAsync())
                .Single(a => a.NewValue!.Contains("\"CoaDisplayOrder\":null"));
            Assert.Contains("\"CoaDisplayOrder\":1", clearAudit.PreviousValue);
        }
    }
}
