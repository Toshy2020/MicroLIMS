using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class AuditEventServiceTests
{
    private readonly MicroLimsDbContext _db;
    private readonly IDatabaseSequenceHelper _sequenceHelper;
    private readonly AuditEventService _service;

    public AuditEventServiceTests()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MicroLimsDbContext(options);
        _sequenceHelper = new DatabaseSequenceHelper(_db);
        _service = new AuditEventService(_db, _sequenceHelper);
    }

    [Fact]
    public async Task RecordUserEventAsync_PopulatesAllExpectedFields()
    {
        _db.CurrentUserId = 42;
        var correlationId = Guid.NewGuid();

        var log = await _service.RecordUserEventAsync(
            actionCode: "DocumentCreated",
            actionCategory: AuditActionCategory.Document,
            recordType: "DocumentMaster",
            documentMasterId: 10,
            documentRevisionId: 101,
            reason: "Initial registration",
            correlationId: correlationId,
            sourceContext: "WebPortal");

        Assert.NotNull(log);
        Assert.StartsWith("EVT-", log.EventUid);
        Assert.Equal(ActorType.User, log.ActorType);
        Assert.Equal(42, log.UserId);
        Assert.Null(log.SystemProcessName);
        Assert.Equal("DocumentCreated", log.ActionCode);
        Assert.Equal(AuditActionCategory.Document, log.ActionCategory);
        Assert.Equal("DocumentMaster", log.EntityName);
        Assert.Equal("10", log.EntityId);
        Assert.Equal(10, log.DocumentMasterId);
        Assert.Equal(101, log.DocumentRevisionId);
        Assert.Equal("Initial registration", log.Reason);
        Assert.Equal(correlationId, log.CorrelationId);
        Assert.Equal("WebPortal", log.SourceContext);
        Assert.True(log.Timestamp <= DateTime.UtcNow);
        Assert.True(log.Timestamp >= DateTime.UtcNow.AddMinutes(-1));

        var saved = await _db.AuditLogs.FirstOrDefaultAsync(l => l.Id == log.Id);
        Assert.NotNull(saved);
        Assert.Equal(42, saved.UserId);
    }

    [Fact]
    public async Task RecordSystemEventAsync_PopulatesAllExpectedFields_WithoutUserId()
    {
        _db.CurrentUserId = 999; // even if db has a user id, system event must not use it

        var correlationId = Guid.NewGuid();

        var log = await _service.RecordSystemEventAsync(
            systemProcessName: "NightlyDocumentExpiryJob",
            actionCode: "RevisionStatusChanged",
            actionCategory: AuditActionCategory.Document,
            recordType: "DocumentRevision",
            documentMasterId: 5,
            documentRevisionId: 55,
            reason: "Automated review expiry",
            correlationId: correlationId,
            sourceContext: "ScheduledJob");

        Assert.NotNull(log);
        Assert.StartsWith("EVT-", log.EventUid);
        Assert.Equal(ActorType.System, log.ActorType);
        Assert.Equal("NightlyDocumentExpiryJob", log.SystemProcessName);
        Assert.Null(log.UserId);
        Assert.Equal("RevisionStatusChanged", log.ActionCode);
        Assert.Equal(AuditActionCategory.Document, log.ActionCategory);
        Assert.Equal("DocumentRevision", log.EntityName);
        Assert.Equal("5", log.EntityId);
        Assert.Equal(5, log.DocumentMasterId);
        Assert.Equal(55, log.DocumentRevisionId);
        Assert.Equal("Automated review expiry", log.Reason);
        Assert.Equal(correlationId, log.CorrelationId);
        Assert.Equal("ScheduledJob", log.SourceContext);

        var saved = await _db.AuditLogs.FirstOrDefaultAsync(l => l.Id == log.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.UserId);
        Assert.Equal("NightlyDocumentExpiryJob", saved.SystemProcessName);
    }

    [Fact]
    public async Task RecordSystemEventAsync_ThrowsIfProcessNameMissing()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RecordSystemEventAsync(
                systemProcessName: "",
                actionCode: "Job",
                actionCategory: AuditActionCategory.Other,
                recordType: "DocumentMaster"));
    }

    [Fact]
    public async Task RecordUserEventAsync_CapturesChildFieldChanges()
    {
        _db.CurrentUserId = 7;
        var changes = new List<AuditFieldChange>
        {
            new("Title", "Draft Specification", "Approved Specification"),
            new("ReviewCycleMonths", "12", "24")
        };

        var log = await _service.RecordUserEventAsync(
            actionCode: "DocumentUpdated",
            actionCategory: AuditActionCategory.Document,
            recordType: "DocumentMaster",
            documentMasterId: 12,
            changes: changes);

        Assert.NotNull(log);
        Assert.Equal(2, log.Changes.Count);

        var titleChange = log.Changes.FirstOrDefault(c => c.FieldName == "Title");
        Assert.NotNull(titleChange);
        Assert.Equal("Draft Specification", titleChange.PreviousValue);
        Assert.Equal("Approved Specification", titleChange.NewValue);

        var cycleChange = log.Changes.FirstOrDefault(c => c.FieldName == "ReviewCycleMonths");
        Assert.NotNull(cycleChange);
        Assert.Equal("12", cycleChange.PreviousValue);
        Assert.Equal("24", cycleChange.NewValue);

        var savedChanges = await _db.AuditEventChanges.Where(c => c.AuditLogId == log.Id).ToListAsync();
        Assert.Equal(2, savedChanges.Count);
    }

    [Fact]
    public async Task ChangeTracker_SafetyNet_CapturesAuditLogForDocumentControlEntities()
    {
        _db.CurrentUserId = 15;

        // 1. Insert entity
        var docType = new DocumentType
        {
            Code = "SOP",
            Name = "Standard Operating Procedure",
            DefaultReviewCycleMonths = 24,
            IsActive = true
        };
        _db.DocumentTypes.Add(docType);
        await _db.SaveChangesAsync();

        var insertLog = await _db.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityName == nameof(DocumentType) && a.Action == "Create");

        Assert.NotNull(insertLog);
        Assert.Equal(15, insertLog.UserId);

        // 2. Update entity
        docType.Name = "Standard Operating Procedure (Revised)";
        await _db.SaveChangesAsync();

        var updateLog = await _db.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityName == nameof(DocumentType) && a.Action == "Update");

        Assert.NotNull(updateLog);
        Assert.Contains("Standard Operating Procedure (Revised)", updateLog.NewValue);
    }
}
