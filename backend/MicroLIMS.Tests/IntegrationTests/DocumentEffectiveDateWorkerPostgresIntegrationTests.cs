using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentEffectiveDateWorkerPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentEffectiveDateWorkerPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int MasterId, int EffectiveRevId, int FutureRevId)> SeedPostgresDocumentAsync(
        MicroLimsDbContext db,
        DateTime futureEffectiveDate)
    {
        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
            db.Roles.Add(analystRole);
            await db.SaveChangesAsync();
        }

        var author = new User
        {
            FullName = "Postgres Worker Author",
            Username = $"pwrk_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = "dummy_hash",
            RoleId = analystRole.Id,
            IsActive = true
        };
        db.Users.Add(author);
        await db.SaveChangesAsync();

        var docType = await db.DocumentTypes.FirstOrDefaultAsync(t => t.Code == "SOP");
        if (docType == null)
        {
            docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
            db.DocumentTypes.Add(docType);
            await db.SaveChangesAsync();
        }

        var dept = await db.DocumentDepartments.FirstOrDefaultAsync(d => d.Code == "QA");
        if (dept == null)
        {
            dept = new DocumentDepartment { Code = "QA", Name = "Quality Assurance", IsActive = true };
            db.DocumentDepartments.Add(dept);
            await db.SaveChangesAsync();
        }

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.DepartmentId == dept.Id);
        if (section == null)
        {
            section = new DocumentSection { Name = "Compliance", DepartmentId = dept.Id, IsActive = true };
            db.DocumentSections.Add(section);
            await db.SaveChangesAsync();
        }

        var uniqueCode = $"SOP-AUTO-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-{Guid.NewGuid():N}".Substring(0, 15),
            CompanyDocumentCode = uniqueCode,
            Title = "Automated Postgres Activation SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = section.Id,
            DocumentOwnerUserId = author.Id,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            CreatedByUserId = author.Id
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        // Effective Revision 01
        var effRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddMonths(-3),
            NextReviewDate = DateTime.UtcNow.AddMonths(21),
            ReviewCycleMonths = 24,
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(effRev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = effRev.Id;
        await db.SaveChangesAsync();

        // Future Effective Revision 02
        var futRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "02",
            RevisionSequence = 2,
            RevisionStatus = DocumentRevisionStatus.FutureEffective,
            EffectiveDate = futureEffectiveDate,
            ReviewCycleMonths = 24,
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(futRev);
        await db.SaveChangesAsync();

        return (master.Id, effRev.Id, futRev.Id);
    }

    [PostgresFact]
    public async Task Postgres_ProcessMaturedRevisions_ExecutesAtomicActivationAndSupersession()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-15);
        var (masterId, effRevId, futRevId) = await SeedPostgresDocumentAsync(db, dueTime);

        var auditService = new AuditEventService(db, new DatabaseSequenceHelper(db));
        var workerService = new DocumentEffectiveDateService(db, auditService, NullLogger<DocumentEffectiveDateService>.Instance);

        // Act
        var result = await workerService.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert
        Assert.True(result.EligibleRevisionsCount >= 1);
        Assert.True(result.SuccessfullyActivatedCount >= 1);

        await using var verifyDb = _fixture.CreateDbContext();
        var refreshedMaster = await verifyDb.DocumentMasters.FindAsync(masterId);
        var refreshedFut = await verifyDb.DocumentRevisions.FindAsync(futRevId);
        var refreshedEff = await verifyDb.DocumentRevisions.FindAsync(effRevId);

        Assert.NotNull(refreshedMaster);
        Assert.Equal(futRevId, refreshedMaster.CurrentEffectiveRevisionId);

        Assert.NotNull(refreshedFut);
        Assert.Equal(DocumentRevisionStatus.Effective, refreshedFut.RevisionStatus);

        Assert.NotNull(refreshedEff);
        Assert.Equal(DocumentRevisionStatus.Superseded, refreshedEff.RevisionStatus);

        // Verify system audit trail created in live PostgreSQL database (DC-URS-180)
        var activationAudit = await verifyDb.AuditLogs
            .FirstOrDefaultAsync(l => l.ActionCode == "RevisionAutomaticallyActivated" && l.DocumentRevisionId == futRevId);
        Assert.NotNull(activationAudit);
        Assert.Equal(ActorType.System, activationAudit.ActorType);
        Assert.Equal("DocumentEffectiveDateWorker", activationAudit.SystemProcessName);
        Assert.Null(activationAudit.UserId);
    }

    [PostgresFact]
    public async Task Postgres_ProcessMaturedRevisions_IsIdempotentOnRepeatedExecution()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        var dueTime = nowUtc.AddMinutes(-5);
        var (masterId, effRevId, futRevId) = await SeedPostgresDocumentAsync(db, dueTime);

        var auditService = new AuditEventService(db, new DatabaseSequenceHelper(db));
        var workerService = new DocumentEffectiveDateService(db, auditService, NullLogger<DocumentEffectiveDateService>.Instance);

        // First Execution
        var result1 = await workerService.ProcessMaturedRevisionsAsync(nowUtc);
        Assert.True(result1.SuccessfullyActivatedCount >= 1);

        // Second Execution
        var result2 = await workerService.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert: revision futRevId was already transitioned; second run does not process it again
        Assert.DoesNotContain(result2.ActivatedTransitions, t => t.ActivatedRevisionId == futRevId);

        await using var verifyDb = _fixture.CreateDbContext();
        var audits = await verifyDb.AuditLogs
            .Where(l => l.ActionCode == "RevisionAutomaticallyActivated" && l.DocumentRevisionId == futRevId)
            .ToListAsync();
        Assert.Single(audits);
    }

    [PostgresFact]
    public async Task Postgres_ProcessMaturedRevisions_DowntimeCatchup_FlagsDelayedExecution()
    {
        await using var db = _fixture.CreateDbContext();
        var nowUtc = DateTime.UtcNow;
        // Due 3 hours ago (simulating server was down for 3 hours DC-URS-181)
        var delayedDue = nowUtc.AddHours(-3);
        var (masterId, effRevId, futRevId) = await SeedPostgresDocumentAsync(db, delayedDue);

        var auditService = new AuditEventService(db, new DatabaseSequenceHelper(db));
        var workerService = new DocumentEffectiveDateService(db, auditService, NullLogger<DocumentEffectiveDateService>.Instance);

        // Act
        var result = await workerService.ProcessMaturedRevisionsAsync(nowUtc);

        // Assert
        var transition = result.ActivatedTransitions.FirstOrDefault(t => t.ActivatedRevisionId == futRevId);
        Assert.NotNull(transition);
        Assert.True(transition.IsDelayedExecution);

        await using var verifyDb = _fixture.CreateDbContext();
        var audit = await verifyDb.AuditLogs
            .Include(a => a.Changes)
            .FirstOrDefaultAsync(l => l.ActionCode == "RevisionAutomaticallyActivated" && l.DocumentRevisionId == futRevId);

        Assert.NotNull(audit);
        Assert.Contains("downtime recovery", audit.Reason);
        Assert.Contains(audit.Changes, c => c.FieldName == "IsDelayedExecution" && c.NewValue == "True");
    }
}
