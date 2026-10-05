using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Tests.UnitTests;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Real transactions (the InMemory provider has none): a failure part-way through a
// titration command must leave neither the change nor its signature/audit behind.
[Collection("PostgresDatabaseCollection")]
public class TitrationAtomicityPostgresTests
{
    private readonly PostgresTestFixture _fixture;
    public TitrationAtomicityPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class ThrowingAudit : IAuditEventService
    {
        public Task<AuditLog> RecordUserEventAsync(string actionCode, AuditActionCategory actionCategory, string recordType,
            int? documentMasterId = null, int? documentRevisionId = null, string? reason = null, Guid? correlationId = null,
            string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null, string? entityId = null,
            CancellationToken cancellationToken = default, int? sampleId = null) =>
            throw new InvalidOperationException("audit failed");

        public Task<AuditLog> RecordSystemEventAsync(string systemProcessName, string actionCode, AuditActionCategory actionCategory,
            string recordType, int? documentMasterId = null, int? documentRevisionId = null, string? reason = null,
            Guid? correlationId = null, string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null,
            string? entityId = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("audit failed");
    }

    // Signs for real, then fails the way a later step of the command would.
    private sealed class FailingResultSignature : IElectronicSignatureService
    {
        private readonly IElectronicSignatureService _inner;
        public FailingResultSignature(IElectronicSignatureService inner) => _inner = inner;
        public async Task<ElectronicSignature> SignAsync(int userId, string password, SignatureMeaning meaning, string entityType,
            int entityId, string? comment, string? ipAddress)
        {
            var sig = await _inner.SignAsync(userId, password, meaning, entityType, entityId, comment, ipAddress);
            if (meaning == SignatureMeaning.ResultRecorded) throw new InvalidOperationException("result signature step failed");
            return sig;
        }
    }

    [PostgresFact]
    public async Task AuditFailure_RollsBackTheTitrationChange()
    {
        await using var db = _fixture.CreateDbContext();
        var s = TitrationScenario.Seed(db, new() { ExistingUserId = _fixture.SeededUserId });
        var svc = TestServiceFactory.TestDefinitionMaster(db, audit: new ThrowingAudit());

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateTestDefinitionAsync(s.UserId, s.Test.Id,
            new UpdateTestDefinitionRequest(s.Test.Code, s.Test.DisplayName, TitrationEquivalencyFactor: 99.99m) { ChangeReason = "should roll back" }));

        await using var v = _fixture.CreateDbContext();
        Assert.Equal(88.06m, (await v.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == s.Test.Id)).TitrationEquivalencyFactor);
    }

    [PostgresFact]
    public async Task LaterFailure_LeavesNoDueAcknowledgementSignature()
    {
        await using var db = _fixture.CreateDbContext();
        var s = TitrationScenario.Seed(db, new()
        {
            ExistingUserId = _fixture.SeededUserId, WithStandard = true, ValidityDays = 1, StandardizedDaysAgo = 5,
            Tweak = t => { t.TitrationCalculation = TitrationCalculation.Relative; t.TitrationEquivalencyFactor = null; t.TitrationBlankRequired = true; },
            TweakSpec = sp => { sp.LowerLimit = 0m; sp.UpperLimit = 1000m; },
        });
        var p = TitrationRecorderTests.Payload(s, standards: new() { new(s.StandardLot!.Id, 100m, 10.00m) }) with
        {
            DueTitrantAcknowledged = true, DueTitrantJustification = "Restandardization booked for tomorrow; factor not used.",
        };
        var engine = TestServiceFactory.TestWorkflow(db,
            signatures: new FailingResultSignature(new MicroLIMS.Application.Services.ElectronicSignatureService(db)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.RecordTitrationResultAsync(s.Order.Id, p, s.UserId, "127.0.0.1"));

        await using var v = _fixture.CreateDbContext();
        Assert.False(await v.ElectronicSignatures.AnyAsync(x => x.EntityType == "TestOrder" && x.EntityId == s.Order.Id));
        Assert.False(await v.TestAnalyses.AnyAsync(x => x.TestOrderId == s.Order.Id));
        Assert.Equal(5m, (await v.Materials.FindAsync(s.StandardLot!.Id))!.QuantityRemaining);
    }
}
