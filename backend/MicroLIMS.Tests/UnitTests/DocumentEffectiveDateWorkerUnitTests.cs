using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.API.BackgroundServices;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentEffectiveDateWorkerUnitTests
{
    private class TestDbSequenceHelper : IDatabaseSequenceHelper
    {
        private long _current = 100;
        public Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Interlocked.Increment(ref _current));
        }
    }

    private async Task<(
        MicroLimsDbContext db,
        User author,
        DocumentMaster master,
        DocumentRevision effectiveRev,
        DocumentRevision futureRev,
        DocumentEffectiveDateService service
    )> CreateSeededContextAsync(DateTime effectiveDate)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new MicroLimsDbContext(options);

        var authorRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        db.Roles.Add(authorRole);
        await db.SaveChangesAsync();

        var author = new User
        {
            FullName = "SOP Author",
            Username = "author1",
            RoleId = authorRole.Id,
            IsActive = true,
            Role = authorRole
        };
        db.Users.Add(author);
        await db.SaveChangesAsync();

        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
        var dept = new DocumentDepartment { Code = "QA", Name = "Quality Assurance", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var section = new DocumentSection { Name = "Compliance", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000101",
            CompanyDocumentCode = "SOP-QA-101",
            Title = "Automated Document Activation Lifecycle SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = section.Id,
            DocumentOwnerUserId = author.Id,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            CreatedByUserId = author.Id
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        // 1. Prior Effective Revision (Rev 01)
        var effectiveRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddMonths(-6),
            NextReviewDate = DateTime.UtcNow.AddMonths(18),
            ReviewCycleMonths = 24,
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(effectiveRev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = effectiveRev.Id;
        await db.SaveChangesAsync();

        // 2. Future Effective Revision (Rev 02)
        var futureRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.FutureEffective,
            EffectiveDate = effectiveDate,
            ReviewCycleMonths = 24,
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(futureRev);
        await db.SaveChangesAsync();

        var auditService = new AuditEventService(db, new TestDbSequenceHelper());
        var service = new DocumentEffectiveDateService(db, auditService, NullLogger<DocumentEffectiveDateService>.Instance);

        return (db, author, master, effectiveRev, futureRev, service);
    }

    [Fact]
    public async Task ProcessMaturedRevisions_WhenFutureEffectiveIsDue_ActivatesAndSupersedesPrior()
    {
        // Arrange: Effective date was 10 minutes ago
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-10);
        var (db, _, master, effectiveRev, futureRev, service) = await CreateSeededContextAsync(dueTime);

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert
        Assert.Equal(1, result.EligibleRevisionsCount);
        Assert.Equal(1, result.SuccessfullyActivatedCount);
        Assert.Equal(0, result.FailedRevisionsCount);

        var refreshedFuture = await db.DocumentRevisions.FindAsync(futureRev.Id);
        var refreshedPrior = await db.DocumentRevisions.FindAsync(effectiveRev.Id);
        var refreshedMaster = await db.DocumentMasters.FindAsync(master.Id);

        Assert.NotNull(refreshedFuture);
        Assert.Equal(DocumentRevisionStatus.Effective, refreshedFuture.RevisionStatus);
        Assert.Equal(futureRev.Id, refreshedMaster!.CurrentEffectiveRevisionId);

        Assert.NotNull(refreshedPrior);
        Assert.Equal(DocumentRevisionStatus.Superseded, refreshedPrior.RevisionStatus);

        // DC-URS-180: System audit log attribution
        var activationAudit = await db.AuditLogs
            .FirstOrDefaultAsync(l => l.ActionCode == "RevisionAutomaticallyActivated" && l.DocumentRevisionId == futureRev.Id);
        Assert.NotNull(activationAudit);
        Assert.Equal(ActorType.System, activationAudit.ActorType);
        Assert.Equal("DocumentEffectiveDateWorker", activationAudit.SystemProcessName);
        Assert.Null(activationAudit.UserId);

        var supersessionAudit = await db.AuditLogs
            .FirstOrDefaultAsync(l => l.ActionCode == "RevisionAutomaticallySuperseded" && l.DocumentRevisionId == effectiveRev.Id);
        Assert.NotNull(supersessionAudit);
        Assert.Equal(ActorType.System, supersessionAudit.ActorType);
        Assert.Equal("DocumentEffectiveDateWorker", supersessionAudit.SystemProcessName);
    }

    [Fact]
    public async Task ProcessMaturedRevisions_WhenNotYetEffective_SkipsActivation()
    {
        // Arrange: Effective date is tomorrow in UTC
        var nowUtc = DateTime.UtcNow;
        var futureDate = nowUtc.AddDays(1);
        var (db, _, master, effectiveRev, futureRev, service) = await CreateSeededContextAsync(futureDate);

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert
        Assert.Equal(0, result.EligibleRevisionsCount);
        Assert.Equal(0, result.SuccessfullyActivatedCount);

        var refreshedFuture = await db.DocumentRevisions.FindAsync(futureRev.Id);
        var refreshedPrior = await db.DocumentRevisions.FindAsync(effectiveRev.Id);
        var refreshedMaster = await db.DocumentMasters.FindAsync(master.Id);

        Assert.Equal(DocumentRevisionStatus.FutureEffective, refreshedFuture!.RevisionStatus);
        Assert.Equal(DocumentRevisionStatus.Effective, refreshedPrior!.RevisionStatus);
        Assert.Equal(effectiveRev.Id, refreshedMaster!.CurrentEffectiveRevisionId);
    }

    [Fact]
    public async Task ProcessMaturedRevisions_AtExactUtcBoundary_ActivatesSuccessfully()
    {
        // Arrange: EffectiveDate matches nowUtc to the exact second (DC-URS-182)
        var exactUtc = new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        var (db, _, master, effectiveRev, futureRev, service) = await CreateSeededContextAsync(exactUtc);

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(exactUtc);

        // Assert
        Assert.Equal(1, result.EligibleRevisionsCount);
        Assert.Equal(1, result.SuccessfullyActivatedCount);

        var refreshedFuture = await db.DocumentRevisions.FindAsync(futureRev.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, refreshedFuture!.RevisionStatus);
    }

    [Fact]
    public async Task ProcessMaturedRevisions_OneSecondBeforeBoundary_DoesNotActivate()
    {
        // Arrange: nowUtc is 1 second before effective boundary
        var targetBoundary = new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        var oneSecondBefore = targetBoundary.AddSeconds(-1);
        var (db, _, _, _, futureRev, service) = await CreateSeededContextAsync(targetBoundary);

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(oneSecondBefore);

        // Assert
        Assert.Equal(0, result.EligibleRevisionsCount);
        var refreshedFuture = await db.DocumentRevisions.FindAsync(futureRev.Id);
        Assert.Equal(DocumentRevisionStatus.FutureEffective, refreshedFuture!.RevisionStatus);
    }

    [Fact]
    public async Task ProcessMaturedRevisions_IsIdempotent_SecondExecutionIsHarmless()
    {
        // Arrange: Mature revision
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-5);
        var (db, _, master, effectiveRev, futureRev, service) = await CreateSeededContextAsync(dueTime);

        // Act 1: First cycle
        var firstResult = await service.ProcessMaturedRevisionsAsync(nowUtc);
        Assert.Equal(1, firstResult.SuccessfullyActivatedCount);

        // Act 2: Second cycle immediately following
        var secondResult = await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert: Second run finds zero eligible records and changes nothing
        Assert.Equal(0, secondResult.EligibleRevisionsCount);
        Assert.Equal(0, secondResult.SuccessfullyActivatedCount);

        // Audit logs count for activation remains exactly 1
        var activationLogs = await db.AuditLogs
            .Where(l => l.ActionCode == "RevisionAutomaticallyActivated" && l.DocumentRevisionId == futureRev.Id)
            .ToListAsync();
        Assert.Single(activationLogs);
    }

    [Fact]
    public async Task ProcessMaturedRevisions_WhenDowntimeDelayExceedsOneHour_FlagsDelayedExecution()
    {
        // Arrange: EffectiveDate was 5 hours ago (simulating application downtime recovery DC-URS-181)
        var nowUtc = DateTime.UtcNow;
        var delayedTime = nowUtc.AddHours(-5);
        var (db, _, _, _, futureRev, service) = await CreateSeededContextAsync(delayedTime);

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert
        Assert.Equal(1, result.SuccessfullyActivatedCount);
        var transition = result.ActivatedTransitions.Single();
        Assert.True(transition.IsDelayedExecution);

        var auditLog = await db.AuditLogs
            .Include(a => a.Changes)
            .FirstOrDefaultAsync(l => l.ActionCode == "RevisionAutomaticallyActivated" && l.DocumentRevisionId == futureRev.Id);
        Assert.NotNull(auditLog);
        Assert.Contains("downtime recovery", auditLog.Reason);

        var delayedChange = auditLog.Changes.FirstOrDefault(c => c.FieldName == "IsDelayedExecution");
        Assert.NotNull(delayedChange);
        Assert.Equal("True", delayedChange.NewValue);
    }

    [Fact]
    public async Task ProcessMaturedRevisions_ProcessesMultipleIndependentDocumentsInBatch()
    {
        // Arrange: Context with document 1
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-10);
        var (db, author, master1, eff1, fut1, service) = await CreateSeededContextAsync(dueTime);

        // Add document 2 with its own effective and future revision
        var docType = await db.DocumentTypes.FirstAsync();
        var dept = await db.DocumentDepartments.FirstAsync();
        var section = await db.DocumentSections.FirstAsync();

        var master2 = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000102",
            CompanyDocumentCode = "SOP-QA-102",
            Title = "Second Document SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = section.Id,
            DocumentOwnerUserId = author.Id,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedByUserId = author.Id
        };
        db.DocumentMasters.Add(master2);
        await db.SaveChangesAsync();

        var eff2 = new DocumentRevision
        {
            DocumentMasterId = master2.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = nowUtc.AddMonths(-3),
            ReviewCycleMonths = 12,
            CreatedByUserId = author.Id,
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(eff2);
        await db.SaveChangesAsync();
        master2.CurrentEffectiveRevisionId = eff2.Id;
        await db.SaveChangesAsync();

        var fut2 = new DocumentRevision
        {
            DocumentMasterId = master2.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.FutureEffective,
            EffectiveDate = dueTime,
            ReviewCycleMonths = 12,
            CreatedByUserId = author.Id,
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(fut2);
        await db.SaveChangesAsync();

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert
        Assert.Equal(2, result.EligibleRevisionsCount);
        Assert.Equal(2, result.SuccessfullyActivatedCount);
        Assert.Equal(0, result.FailedRevisionsCount);

        var refFut1 = await db.DocumentRevisions.FindAsync(fut1.Id);
        var refFut2 = await db.DocumentRevisions.FindAsync(fut2.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, refFut1!.RevisionStatus);
        Assert.Equal(DocumentRevisionStatus.Effective, refFut2!.RevisionStatus);

        var refEff1 = await db.DocumentRevisions.FindAsync(eff1.Id);
        var refEff2 = await db.DocumentRevisions.FindAsync(eff2.Id);
        Assert.Equal(DocumentRevisionStatus.Superseded, refEff1!.RevisionStatus);
        Assert.Equal(DocumentRevisionStatus.Superseded, refEff2!.RevisionStatus);
    }

    [Fact]
    public async Task Worker_CannotActivateDraft_InReview_Or_AwaitingApprovalRevisions()
    {
        // Arrange
        var nowUtc = DateTime.UtcNow;
        var pastDate = nowUtc.AddDays(-2);
        var (db, author, master, _, _, service) = await CreateSeededContextAsync(pastDate);

        var draftRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "03",
            RevisionSequence = 3,
            RevisionStatus = DocumentRevisionStatus.Draft,
            EffectiveDate = pastDate,
            CreatedByUserId = author.Id
        };
        var inReviewRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "04",
            RevisionSequence = 4,
            RevisionStatus = DocumentRevisionStatus.InReview,
            EffectiveDate = pastDate,
            CreatedByUserId = author.Id
        };
        var awaitingRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "05",
            RevisionSequence = 5,
            RevisionStatus = DocumentRevisionStatus.AwaitingApproval,
            EffectiveDate = pastDate,
            CreatedByUserId = author.Id
        };

        db.DocumentRevisions.AddRange(draftRev, inReviewRev, awaitingRev);
        await db.SaveChangesAsync();

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert: Only the pre-seeded futureRev is eligible; Draft/InReview/AwaitingApproval are completely ignored
        Assert.Equal(1, result.EligibleRevisionsCount);

        var refDraft = await db.DocumentRevisions.FindAsync(draftRev.Id);
        var refReview = await db.DocumentRevisions.FindAsync(inReviewRev.Id);
        var refAwaiting = await db.DocumentRevisions.FindAsync(awaitingRev.Id);

        Assert.Equal(DocumentRevisionStatus.Draft, refDraft!.RevisionStatus);
        Assert.Equal(DocumentRevisionStatus.InReview, refReview!.RevisionStatus);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, refAwaiting!.RevisionStatus);
    }

    [Fact]
    public async Task BackgroundWorker_RunCycleAsync_ExecutesCleanlyThroughServiceScope()
    {
        // Arrange
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-10);
        var (db, _, _, _, futureRev, service) = await CreateSeededContextAsync(dueTime);

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IDocumentEffectiveDateService>(service);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var config = new ConfigurationBuilder().Build();
        var worker = new DocumentEffectiveDateWorker(scopeFactory, config, NullLogger<DocumentEffectiveDateWorker>.Instance);

        // Act: Invoke single cycle
        await worker.RunCycleAsync();

        // Assert
        var refreshed = await db.DocumentRevisions.FindAsync(futureRev.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, refreshed!.RevisionStatus);
    }

    [Fact]
    public async Task Worker_WhenDocumentHasCorruptData_LogsSystemProcessErrorAndContinuesOtherDocuments()
    {
        // Arrange: Document 1 is valid, Document 2 has missing master
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-10);
        var (db, author, master1, eff1, fut1, service) = await CreateSeededContextAsync(dueTime);

        // Corrupt future revision: DocumentMaster is voided / record status is invalid or missing required references
        var corruptMaster = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-CORRUPT",
            CompanyDocumentCode = "SOP-CORRUPT",
            Title = "Corrupted Master SOP",
            DocumentTypeId = 999999, // Non-existent doc type
            DepartmentId = master1.DepartmentId,
            SectionId = master1.SectionId,
            DocumentOwnerUserId = author.Id,
            RecordStatus = DocumentRecordStatus.Void, // Void master
            CreatedByUserId = author.Id
        };
        db.DocumentMasters.Add(corruptMaster);
        await db.SaveChangesAsync();

        var corruptRev = new DocumentRevision
        {
            DocumentMasterId = corruptMaster.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.FutureEffective,
            EffectiveDate = dueTime,
            CreatedByUserId = author.Id
        };
        db.DocumentRevisions.Add(corruptRev);
        await db.SaveChangesAsync();

        // Act
        var result = await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert: 2 eligible, 1 successfully activated, 1 failed, 0 exceptions propagated
        Assert.Equal(2, result.EligibleRevisionsCount);
        Assert.Equal(1, result.SuccessfullyActivatedCount);
        Assert.Equal(1, result.FailedRevisionsCount);

        var failure = result.Failures.Single();
        Assert.Equal(corruptRev.Id, failure.DocumentRevisionId);
        Assert.Contains("Void", failure.ErrorMessage);

        // Verify valid document 1 was successfully committed despite document 2 failure
        var refFut1 = await db.DocumentRevisions.FindAsync(fut1.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, refFut1!.RevisionStatus);

        // Verify SystemProcessError audit log was emitted (DC-URS-183)
        var errorAudit = await db.AuditLogs
            .FirstOrDefaultAsync(l => l.ActionCode == "SystemProcessError" && l.DocumentRevisionId == corruptRev.Id);
        Assert.NotNull(errorAudit);
        Assert.Equal(ActorType.System, errorAudit.ActorType);
        Assert.Equal("DocumentEffectiveDateWorker", errorAudit.SystemProcessName);
    }

    [Fact]
    public async Task Worker_NeverLeavesTwoEffectiveRevisionsForSameDocumentMaster()
    {
        // Arrange
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-10);
        var (db, _, master, eff1, fut1, service) = await CreateSeededContextAsync(dueTime);

        // Act
        await service.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert
        var allRevisions = await db.DocumentRevisions
            .Where(r => r.DocumentMasterId == master.Id)
            .ToListAsync();

        var effectiveRevisions = allRevisions.Where(r => r.RevisionStatus == DocumentRevisionStatus.Effective).ToList();
        Assert.Single(effectiveRevisions);
        Assert.Equal(fut1.Id, effectiveRevisions[0].Id);

        var supersededRevisions = allRevisions.Where(r => r.RevisionStatus == DocumentRevisionStatus.Superseded).ToList();
        Assert.Single(supersededRevisions);
        Assert.Equal(eff1.Id, supersededRevisions[0].Id);
    }
}

