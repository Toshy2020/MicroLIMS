using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// On PostgreSQL a new row has only a temporary key (a large negative
// number) until it is inserted, and so does every foreign key pointing at
// it. The automatic audit trail used to read them before the insert, so
// every "Create" entry named a record that does not exist. The in-memory
// provider hands out real keys up front, which is why only a real
// database shows this.
[Collection("PostgresDatabaseCollection")]
public class AuditTrailRecordIdsPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public AuditTrailRecordIdsPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueCode() => "AUD-" + Guid.NewGuid().ToString("N")[..10];

    private static async Task<List<AuditLog>> AuditRowsAsync(Microsoft.EntityFrameworkCore.DbContext db, string entityName, IEnumerable<int> ids)
    {
        var idStrings = ids.Select(i => i.ToString()).ToList();
        return await db.Set<AuditLog>()
            .Where(a => a.EntityName == entityName && idStrings.Contains(a.EntityId))
            .OrderBy(a => a.Id)
            .ToListAsync();
    }

    private static JsonElement Values(string json) => JsonDocument.Parse(json).RootElement;

    [PostgresFact]
    public async Task CreatedRecords_AreAuditedUnderTheirRealIds_IncludingForeignKeysToOtherNewRows()
    {
        await using var db = _fixture.CreateDbContext();
        var item = new Item
        {
            Code = UniqueCode(),
            Name = "Audit id probe",
            AssignedTests = { new SampleTest { TestCode = "TAMC", DisplayName = "TAMC" }, new SampleTest { TestCode = "TYMC", DisplayName = "TYMC" } }
        };

        db.Items.Add(item);
        await db.SaveChangesAsync();

        var itemCreate = Assert.Single(await AuditRowsAsync(db, nameof(Item), new[] { item.Id }));
        Assert.Equal("Create", itemCreate.Action);
        Assert.Equal(item.Id, Values(itemCreate.NewValue!).GetProperty("Id").GetInt32());

        var testIds = item.AssignedTests.Select(t => t.Id).ToList();
        var testCreates = await AuditRowsAsync(db, nameof(SampleTest), testIds);
        Assert.Equal(2, testCreates.Count);
        foreach (var row in testCreates)
        {
            Assert.Equal("Create", row.Action);
            var values = Values(row.NewValue!);
            Assert.Equal(row.EntityId, values.GetProperty("Id").GetInt32().ToString());
            // The foreign key to the Item created in the same save.
            Assert.Equal(item.Id, values.GetProperty("ItemId").GetInt32());
        }

        Assert.DoesNotContain(await db.AuditLogs.Where(a => a.EntityName == nameof(SampleTest) || a.EntityName == nameof(Item)).Select(a => a.EntityId).ToListAsync(),
            id => id.StartsWith("-"));
    }

    [PostgresFact]
    public async Task UpdatesAndDeletes_StillRecordTheValuesBeforeTheChange()
    {
        await using var db = _fixture.CreateDbContext();
        var item = new Item { Code = UniqueCode(), Name = "Before", AssignedTests = { new SampleTest { TestCode = "TAMC", DisplayName = "TAMC" } } };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        var testId = item.AssignedTests[0].Id;

        item.Name = "After";
        db.Remove(item.AssignedTests[0]);
        await db.SaveChangesAsync();

        var update = (await AuditRowsAsync(db, nameof(Item), new[] { item.Id })).Single(a => a.Action == "Update");
        Assert.Equal("Before", Values(update.PreviousValue!).GetProperty("Name").GetString());
        Assert.Equal("After", Values(update.NewValue!).GetProperty("Name").GetString());

        var delete = (await AuditRowsAsync(db, nameof(SampleTest), new[] { testId })).Single(a => a.Action == "Delete");
        Assert.Equal(item.Id, Values(delete.PreviousValue!).GetProperty("ItemId").GetInt32());
        Assert.Null(delete.NewValue);
    }

    // Fails the audit-only save that follows the data save.
    private sealed class FailAuditSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var changed = eventData.Context!.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList();
            if (changed.Count > 0 && changed.All(e => e.Entity is AuditLog))
                throw new InvalidOperationException("audit write failed");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [PostgresFact]
    public async Task WhenTheAuditRowsCannotBeWritten_TheChangeIsNotCommittedEither()
    {
        var code = UniqueCode();
        await using (var db = _fixture.CreateDbContext(new FailAuditSaveInterceptor()))
        {
            db.Items.Add(new Item { Code = code, Name = "Never audited" });
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }

        await using var check = _fixture.CreateDbContext();
        Assert.False(await check.Items.AnyAsync(i => i.Code == code));
    }
}
