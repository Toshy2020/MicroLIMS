using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlPeriodicReviewPostgresIntegrationTests
{
    private const string Password = "PeriodicReview-Postgres-2026!";
    private readonly PostgresTestFixture _fixture;

    public DocumentControlPeriodicReviewPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(int AuthorId, int ReviewerId, int ApproverId, int MasterId, int RevisionId)> SeedEffectiveDocumentAsync(
        MicroLimsDbContext db,
        DateTime? nextReviewDate = null)
    {
        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
            db.Roles.Add(analystRole);
            await db.SaveChangesAsync();
        }

        var reviewerRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Reviewer);
        if (reviewerRole == null)
        {
            reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true };
            db.Roles.Add(reviewerRole);
            await db.SaveChangesAsync();
        }

        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

        var author = new User
        {
            FullName = "Postgres PR Author",
            Username = $"pr_a_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = analystRole.Id,
            IsActive = true
        };
        var reviewer = new User
        {
            FullName = "Postgres PR Reviewer",
            Username = $"pr_r_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = reviewerRole.Id,
            IsActive = true
        };
        var approver = new User
        {
            FullName = "Postgres PR Approver",
            Username = $"pr_app_{Guid.NewGuid():N}".Substring(0, 15),
            PasswordHash = defaultPasswordHash,
            RoleId = reviewerRole.Id,
            IsActive = true
        };

        db.Users.AddRange(author, reviewer, approver);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-PR-{Guid.NewGuid():N}".Substring(0, 15).ToUpperInvariant(),
            CompanyDocumentCode = $"SOP-PR-PG-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            Title = "Live Postgres Periodic Review SOP",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = author.Id,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordStatus = DocumentRecordStatus.Active,
            RecordOrigin = RecordOrigin.Native,
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-12)
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            ReviewCycleMonths = 12,
            EffectiveDate = DateTime.UtcNow.AddMonths(-12),
            NextReviewDate = nextReviewDate ?? DateTime.UtcNow.AddDays(-5),
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-12)
        };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = revision.Id;
        await db.SaveChangesAsync();

        var pdfFile = new RevisionFile
        {
            DocumentRevisionId = revision.Id,
            FileRole = FileRole.ControlledPdf,
            StorageKey = $"pg-controlled-pdf-{revision.Id}.pdf",
            FileName = "pg_controlled_sop.pdf",
            ContentType = "application/pdf",
            SizeBytes = 45000,
            ContentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            IsActive = true,
            UploadedAt = DateTime.UtcNow.AddMonths(-12),
            UploadedByUserId = author.Id
        };
        db.RevisionFiles.Add(pdfFile);
        await db.SaveChangesAsync();

        return (author.Id, reviewer.Id, approver.Id, master.Id, revision.Id);
    }

    private PeriodicReviewService CreateService(MicroLimsDbContext db)
    {
        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);

        return new PeriodicReviewService(
            db,
            audit,
            auth,
            approvalService,
            new NullLogger<PeriodicReviewService>()
        );
    }

    [Fact]
    public async Task Postgres_GenerateDueReviewTasks_PersistsTaskAndLogsSystemAudit()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, _, _, masterId, revisionId) = await SeedEffectiveDocumentAsync(db, DateTime.UtcNow.AddDays(-2));
        var service = CreateService(db);

        var result = await service.GenerateDueReviewTasksAsync();

        Assert.True(result.CreatedTasksCount >= 1);

        var task = await db.PeriodicReviewTasks
            .Include(t => t.DocumentMaster)
            .Include(t => t.DocumentRevision)
            .FirstOrDefaultAsync(t => t.DocumentRevisionId == revisionId);

        Assert.NotNull(task);
        Assert.Equal(masterId, task.DocumentMasterId);
        Assert.Equal(PeriodicReviewTaskStatus.Pending, task.Status);
        Assert.Equal(12, task.ReviewCycleMonths);

        // Verify audit event in PostgreSQL attributed to System
        var auditLog = await db.AuditLogs
            .Where(a => a.DocumentMasterId == masterId && a.ActionCode == "PeriodicReviewTaskCreated")
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(auditLog);
        Assert.Equal(ActorType.System, auditLog.ActorType);
    }

    [Fact]
    public async Task Postgres_GenerateDueReviewTasks_IsStrictlyIdempotent()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, _, _, _, revisionId) = await SeedEffectiveDocumentAsync(db, DateTime.UtcNow.AddDays(-10));
        var service = CreateService(db);

        // Run cycle 1
        var result1 = await service.GenerateDueReviewTasksAsync();
        Assert.True(result1.CreatedTasksCount >= 1);

        // Run cycle 2 immediately
        var result2 = await service.GenerateDueReviewTasksAsync();

        // Task count for this revision must remain exactly 1
        var taskCount = await db.PeriodicReviewTasks.CountAsync(t => t.DocumentRevisionId == revisionId);
        Assert.Equal(1, taskCount);
    }

    [Fact]
    public async Task Postgres_CompleteReview_RemainsValid_AdvancesNextReviewDateInPostgres()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, reviewerId, _, masterId, revisionId) = await SeedEffectiveDocumentAsync(db, DateTime.UtcNow.AddDays(-1));
        var service = CreateService(db);

        await service.GenerateDueReviewTasksAsync();
        var task = await db.PeriodicReviewTasks.FirstAsync(t => t.DocumentRevisionId == revisionId);

        // Add page/section finding
        await service.AddFindingAsync(task.Id, new CreatePeriodicReviewFindingRequest(
            PageNumber: 1,
            SectionNumber: "3.2",
            NoteText: "Confirmed all steps remain compliant with 2026 ISO standards."
        ), reviewerId);

        // Complete review as RemainsValid
        var completedTask = await service.CompleteReviewAsync(task.Id, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.RemainsValid,
            ReviewSummary: "Periodic review completed. Method and references remain scientifically valid."
        ), reviewerId);

        Assert.Equal(PeriodicReviewTaskStatus.Completed, completedTask.Status);
        Assert.Equal(PeriodicReviewOutcome.RemainsValid, completedTask.Outcome);

        // Verify revision in database
        var refreshedRev = await db.DocumentRevisions.FindAsync(revisionId);
        Assert.NotNull(refreshedRev);
        Assert.Equal(DocumentRevisionStatus.Effective, refreshedRev.RevisionStatus);
        Assert.True(refreshedRev.NextReviewDate > DateTime.UtcNow.AddMonths(11));

        // Verify NO new revision was created in master
        var totalRevs = await db.DocumentRevisions.CountAsync(r => r.DocumentMasterId == masterId);
        Assert.Equal(1, totalRevs);

        // Verify audit event
        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.DocumentMasterId == masterId && a.ActionCode == "PeriodicReviewRemainsValid");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task Postgres_CompleteReview_ObsolescenceRecommended_CreatesApprovalTaskInPostgres()
    {
        await using var db = _fixture.CreateDbContext();
        var (_, reviewerId, _, _, revisionId) = await SeedEffectiveDocumentAsync(db, DateTime.UtcNow.AddDays(-1));
        var service = CreateService(db);

        await service.GenerateDueReviewTasksAsync();
        var task = await db.PeriodicReviewTasks.FirstAsync(t => t.DocumentRevisionId == revisionId);

        // Complete review as ObsolescenceRecommended
        await service.CompleteReviewAsync(task.Id, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.ObsolescenceRecommended,
            ReviewSummary: "Reagent replaced by automated equipment; this SOP is recommended for obsolescence."
        ), reviewerId);

        // Verify approval task was inserted into postgres
        var approvalTask = await db.DocumentApprovalTasks
            .FirstOrDefaultAsync(t => t.DocumentRevisionId == revisionId);

        Assert.NotNull(approvalTask);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, approvalTask.Status);

        // Verify revision remains Effective pending formal approval sign-off
        var rev = await db.DocumentRevisions.FindAsync(revisionId);
        Assert.Equal(DocumentRevisionStatus.Effective, rev!.RevisionStatus);
    }

    [Fact]
    public async Task Postgres_AuthorCannotReviewOrComplete_EnforcesSegregationOfDuties()
    {
        await using var db = _fixture.CreateDbContext();
        var (authorId, _, _, _, revisionId) = await SeedEffectiveDocumentAsync(db, DateTime.UtcNow.AddDays(-1));
        var service = CreateService(db);

        await service.GenerateDueReviewTasksAsync();
        var task = await db.PeriodicReviewTasks.FirstAsync(t => t.DocumentRevisionId == revisionId);

        // Author attempting to add finding
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddFindingAsync(task.Id, new CreatePeriodicReviewFindingRequest(
                PageNumber: 1,
                SectionNumber: "1.0",
                NoteText: "Author note"
            ), authorId));

        // Author attempting to complete review
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CompleteReviewAsync(task.Id, new CompletePeriodicReviewRequest(
                Outcome: PeriodicReviewOutcome.RemainsValid,
                ReviewSummary: "Author self-review"
            ), authorId));
    }
}