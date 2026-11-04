using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class NotApplicableTestPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public NotApplicableTestPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed class ThrowingAuditService : IAuditEventService
    {
        public Task<AuditLog> RecordUserEventAsync(string actionCode, AuditActionCategory actionCategory, string recordType, int? documentMasterId = null, int? documentRevisionId = null, string? reason = null, Guid? correlationId = null, string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null, string? entityId = null, CancellationToken cancellationToken = default, int? sampleId = null) =>
            throw new InvalidOperationException("Audit write failure simulated.");

        public Task<AuditLog> RecordSystemEventAsync(string systemProcessName, string actionCode, AuditActionCategory actionCategory, string recordType, int? documentMasterId = null, int? documentRevisionId = null, string? reason = null, Guid? correlationId = null, string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null, string? entityId = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Audit write failure simulated.");
    }

    [PostgresFact]
    public async Task Create_WhenAuditWriteThrows_RollsBackTransactionAndLeavesNoRecord()
    {
        await using var db = _fixture.CreateDbContext();
        var testCode = UniqueCode("NA-");
        var item = new Item
        {
            Code = UniqueCode("ITM-"),
            Name = "Item for NA rollback test",
            Category = SampleCategory.RawMaterial,
            IsActive = true
        };
        var def = new TestDefinition
        {
            Code = testCode,
            DisplayName = "Test for NA rollback",
            SectionId = _fixture.SeededSectionId
        };
        db.TestDefinitions.Add(def);
        item.AssignedTests.Add(new SampleTest { TestCode = testCode, DisplayName = "Test for NA rollback" });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var service = new NotApplicableTestService(
            db,
            new ElectronicSignatureService(db),
            new ThrowingAuditService());

        var request = new CreateNotApplicableTestRequest(
            ItemId: item.Id,
            Grade: null,
            TestCode: testCode,
            Justification: "Test justification",
            Criticality: "Non-critical",
            ImmediateAction: "Skip",
            LongTermAction: "Review",
            Reference: "REF-01");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(_fixture.SeededUserId, request));

        await using var check = _fixture.CreateDbContext();
        var records = await check.NotApplicableTests.Where(n => n.ItemId == item.Id).ToListAsync();
        Assert.Empty(records);
    }
}
