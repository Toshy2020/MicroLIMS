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
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class DocumentControlQaApproverPostgresTests
{
    private const string TestPassword = "Qa-Approver-Test-1!";
    private const string ExpectedQaRequiredMessage = "This document type requires approval by Quality Assurance.";
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestFixture _fixture;

    public DocumentControlQaApproverPostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Role> EnsureQaRoleWithGrantsAsync(MicroLimsDbContext db)
    {
        // Always its own role: the fixture database is shared, and adding users to a QA role
        // another test created (with Access.Review.Sign) would turn them into access review signers.
        var qaRole = new Role
        {
            Type = RoleType.Qa,
            Name = $"QA-doc-{Guid.NewGuid():N}",
            IsSystemRole = false,
            IsActive = true
        };
        db.Roles.Add(qaRole);
        await db.SaveChangesAsync();

        var existingCodes = await db.RolePermissions
            .Where(rp => rp.RoleId == qaRole.Id)
            .Select(rp => rp.Permission!.Code)
            .ToListAsync();

        // Access.Review.Sign is left out on purpose: the fixture database is shared, and an active
        // holder of that code switches access review signing over to QA for every later test.
        var missingCodes = PermissionConstants.QaGrants
            .Where(c => c != PermissionConstants.AccessReviewSign)
            .Except(existingCodes, StringComparer.Ordinal).ToList();
        if (missingCodes.Count > 0)
        {
            var permissions = await db.Permissions
                .Where(p => missingCodes.Contains(p.Code))
                .ToListAsync();

            foreach (var code in missingCodes)
            {
                var perm = permissions.FirstOrDefault(p => p.Code == code);
                if (perm is null)
                {
                    perm = new Permission { Code = code, Description = code, IsEnforced = true };
                    db.Permissions.Add(perm);
                    await db.SaveChangesAsync();
                }

                db.RolePermissions.Add(new RolePermission { RoleId = qaRole.Id, PermissionId = perm.Id });
            }

            await db.SaveChangesAsync();
        }

        return qaRole;
    }

    private static async Task<int> CreateUserAsync(MicroLimsDbContext db, RoleType roleType, string prefix)
    {
        var role = roleType == RoleType.Qa
            ? await EnsureQaRoleWithGrantsAsync(db)
            : await db.Roles.FirstAsync(r => r.Type == roleType);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new User
        {
            FullName = $"{prefix} {suffix}",
            Username = $"{prefix}_{suffix}".ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash(TestPassword),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(int AuthorId, int ReviewerId, int QaUserId, int MasterId, int RevisionId)> SeedAwaitingApprovalRevisionAsync(
        MicroLimsDbContext db,
        int? docTypeId = null,
        int? authorOverrideId = null)
    {
        var authorId = authorOverrideId ?? await CreateUserAsync(db, RoleType.Analyst, "doc_author");
        var reviewerId = await CreateUserAsync(db, RoleType.Reviewer, "doc_reviewer");
        var qaUserId = await CreateUserAsync(db, RoleType.Qa, "doc_qa");

        var now = FixedNow.UtcDateTime;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var effectiveDocTypeId = docTypeId ?? _fixture.SeededDocTypeId;

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-QA-{suffix}",
            CompanyDocumentCode = $"SOP-QA-{suffix}",
            Title = $"QA Approver Test Document {suffix}",
            DocumentTypeId = effectiveDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = _fixture.SeededControllerUserId,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordOrigin = RecordOrigin.Native,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedAt = now.AddDays(-2),
            CreatedByUserId = authorId
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.AwaitingApproval,
            RevisionType = RevisionType.Major,
            ReasonForRevision = "Baseline creation for QA approver verification.",
            ReviewCycleMonths = 24,
            RecordOrigin = RecordOrigin.Native,
            CreatedAt = now.AddDays(-2),
            CreatedByUserId = authorId
        };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 QA Approver Test Controlled PDF");
        db.RevisionFiles.Add(new RevisionFile
        {
            DocumentRevisionId = revision.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = $"qa_test_{suffix}.pdf",
            ContentType = "application/pdf",
            SizeBytes = pdfBytes.Length,
            ContentSha256 = $"sha_{suffix}",
            IsActive = true,
            UploadedByUserId = authorId,
            UploadedAt = now.AddDays(-1)
        });

        db.DocumentReviewTasks.Add(new DocumentReviewTask
        {
            DocumentRevisionId = revision.Id,
            AssignedReviewerUserId = reviewerId,
            AssignedByUserId = authorId,
            AssignedAt = now.AddDays(-1),
            Status = ReviewTaskStatus.Completed,
            Decision = ReviewDecision.CompleteReview,
            DecisionAt = now.AddHours(-12),
            DecisionByUserId = reviewerId,
            ReviewNotes = "Technical review completed cleanly."
        });

        await db.SaveChangesAsync();
        return (authorId, reviewerId, qaUserId, master.Id, revision.Id);
    }

    private static DocumentApprovalService CreateApprovalService(MicroLimsDbContext db, TimeProvider? clock = null)
    {
        var effectiveClock = clock ?? new FixedTimeProvider(FixedNow);
        var seq = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seq, effectiveClock);
        var auth = new DocumentAuthorizationService(db);
        var signature = new ElectronicSignatureService(db, effectiveClock);
        return new DocumentApprovalService(db, audit, auth, signature, effectiveClock);
    }

    private static DocumentConfigurationService CreateConfigService(MicroLimsDbContext db, TimeProvider? clock = null)
    {
        var effectiveClock = clock ?? new FixedTimeProvider(FixedNow);
        var seq = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seq, effectiveClock);
        var auth = new DocumentAuthorizationService(db);
        return new DocumentConfigurationService(db, audit, auth, effectiveClock);
    }

    [PostgresFact]
    public async Task Qa_can_be_assigned_as_approver_and_approve_with_signature()
    {
        int qaUserId;
        int masterId;
        int revisionId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            (_, _, qaUserId, masterId, revisionId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb);
        }

        int taskId;
        await using (var actAssignDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var service = CreateApprovalService(actAssignDb);
            var created = await service.CreateApprovalTaskAsync(
                revisionId,
                new CreateApprovalTaskRequest(qaUserId, SubmissionNotes: "Assign QA approver"),
                _fixture.SeededControllerUserId);
            taskId = created.Id;
        }

        await using (var actApproveDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var service = CreateApprovalService(actApproveDb);
            var approved = await service.ExecuteApprovalDecisionAsync(
                taskId,
                new ExecuteApprovalDecisionRequest(
                    DocumentApprovalDecision.Approve,
                    DecisionNotes: "Approved by Quality Assurance.",
                    Password: TestPassword),
                qaUserId);
            Assert.Equal(DocumentApprovalTaskStatus.Approved, approved.Status);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var task = await assertDb.DocumentApprovalTasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
        Assert.Equal(DocumentApprovalTaskStatus.Approved, task.Status);
        Assert.Equal(DocumentApprovalDecision.Approve, task.Decision);
        Assert.Equal(qaUserId, task.AssignedApproverUserId);
        Assert.Equal(qaUserId, task.DecisionByUserId);

        var revision = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revisionId);
        Assert.Equal(DocumentRevisionStatus.Effective, revision.RevisionStatus);
        Assert.NotNull(revision.EffectiveDate);

        var master = await assertDb.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == masterId);
        Assert.Equal(revisionId, master.CurrentEffectiveRevisionId);

        var sig = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s =>
            s.EntityType == "DocumentRevision" &&
            s.EntityId == revisionId &&
            s.MeaningOfSignature == SignatureMeaning.Approved);
        Assert.Equal(qaUserId, sig.UserId);
        Assert.Equal(nameof(RoleType.Qa), sig.RoleSnapshot);

        var audit = await assertDb.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a =>
            a.DocumentRevisionId == revisionId &&
            a.ActionCode == "DocumentRevisionApproved");
        Assert.NotNull(audit);
        Assert.Equal(qaUserId, audit.UserId);
    }

    [PostgresFact]
    public async Task Qa_author_cannot_approve_own_revision()
    {
        int qaAuthorId;
        int revisionId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            qaAuthorId = await CreateUserAsync(arrangeDb, RoleType.Qa, "qa_author");
            (_, _, _, _, revisionId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb, authorOverrideId: qaAuthorId);
        }

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var service = CreateApprovalService(actDb);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.CreateApprovalTaskAsync(
                    revisionId,
                    new CreateApprovalTaskRequest(qaAuthorId),
                    _fixture.SeededControllerUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.DocumentApprovalTasks.AsNoTracking().AnyAsync(t => t.DocumentRevisionId == revisionId));
        var revision = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revisionId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revision.RevisionStatus);
    }

    [PostgresFact]
    public async Task Analyst_still_not_eligible_as_approver()
    {
        int analystId;
        int revisionId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            analystId = await CreateUserAsync(arrangeDb, RoleType.Analyst, "analyst_cand");
            (_, _, _, _, revisionId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb);
        }

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var service = CreateApprovalService(actDb);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateApprovalTaskAsync(
                    revisionId,
                    new CreateApprovalTaskRequest(analystId),
                    _fixture.SeededControllerUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.DocumentApprovalTasks.AsNoTracking().AnyAsync(t => t.DocumentRevisionId == revisionId));
    }

    [PostgresFact]
    public async Task Reviewer_and_section_head_eligibility_unchanged()
    {
        int reviewerApproverId;
        int sectionHeadApproverId;
        int rev1Id;
        int rev2Id;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            reviewerApproverId = await CreateUserAsync(arrangeDb, RoleType.Reviewer, "rev_approver");
            sectionHeadApproverId = await CreateUserAsync(arrangeDb, RoleType.SectionHead, "sh_approver");
            (_, _, _, _, rev1Id) = await SeedAwaitingApprovalRevisionAsync(arrangeDb);
            (_, _, _, _, rev2Id) = await SeedAwaitingApprovalRevisionAsync(arrangeDb);
        }

        int task1Id;
        int task2Id;
        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var service = CreateApprovalService(actDb);
            var t1 = await service.CreateApprovalTaskAsync(rev1Id, new CreateApprovalTaskRequest(reviewerApproverId), _fixture.SeededControllerUserId);
            var t2 = await service.CreateApprovalTaskAsync(rev2Id, new CreateApprovalTaskRequest(sectionHeadApproverId), _fixture.SeededControllerUserId);
            task1Id = t1.Id;
            task2Id = t2.Id;

            await service.ExecuteApprovalDecisionAsync(task1Id,
                new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Reviewer approved.", Password: TestPassword),
                reviewerApproverId);
            await service.ExecuteApprovalDecisionAsync(task2Id,
                new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "SectionHead approved.", Password: TestPassword),
                sectionHeadApproverId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var rev1 = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == rev1Id);
        var rev2 = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == rev2Id);
        Assert.Equal(DocumentRevisionStatus.Effective, rev1.RevisionStatus);
        Assert.Equal(DocumentRevisionStatus.Effective, rev2.RevisionStatus);
    }

    [PostgresFact]
    public async Task Qa_gets_no_section_head_override()
    {
        int assignedReviewerId;
        int qaUserId;
        int revisionId;
        int taskId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            assignedReviewerId = await CreateUserAsync(arrangeDb, RoleType.Reviewer, "assigned_rev");
            (_, _, qaUserId, _, revisionId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb);

            var service = CreateApprovalService(arrangeDb);
            var createdTask = await service.CreateApprovalTaskAsync(
                revisionId,
                new CreateApprovalTaskRequest(assignedReviewerId),
                _fixture.SeededControllerUserId);
            taskId = createdTask.Id;
        }

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var service = CreateApprovalService(actDb);
            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
                service.ExecuteApprovalDecisionAsync(
                    taskId,
                    new ExecuteApprovalDecisionRequest(
                        DocumentApprovalDecision.Approve,
                        "QA attempting override on behalf of reviewer.",
                        Password: TestPassword),
                    qaUserId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var task = await assertDb.DocumentApprovalTasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, task.Status);
        Assert.Null(task.DecisionByUserId);

        var revision = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revisionId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revision.RevisionStatus);

        Assert.False(await assertDb.ElectronicSignatures.AsNoTracking()
            .AnyAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revisionId));
    }

    [PostgresFact]
    public async Task Type_requiring_qa_refuses_approval_route_without_qa_approver()
    {
        int docTypeId;
        int nonQaApproverId;
        int revisionId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var config = CreateConfigService(arrangeDb);
            var code = $"QAR_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            var docType = await config.CreateDocumentTypeAsync(
                new CreateDocumentTypeRequest(code, $"QA Required Type {code}", 24, RequiresQaApprover: true),
                _fixture.SeededUserId);
            docTypeId = docType.Id;

            nonQaApproverId = await CreateUserAsync(arrangeDb, RoleType.Reviewer, "non_qa_app");
            (_, _, _, _, revisionId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb, docTypeId: docTypeId);
        }

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var service = CreateApprovalService(actDb);
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateApprovalTaskAsync(
                    revisionId,
                    new CreateApprovalTaskRequest(nonQaApproverId),
                    _fixture.SeededControllerUserId));
            Assert.Equal(ExpectedQaRequiredMessage, ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.DocumentApprovalTasks.AsNoTracking().AnyAsync(t => t.DocumentRevisionId == revisionId));
        var revision = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revisionId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revision.RevisionStatus);
    }

    [PostgresFact]
    public async Task Type_requiring_qa_does_not_become_effective_until_the_qa_approver_has_approved()
    {
        int docTypeId;
        int nonQaApproverId;
        int qaUserId;
        int revApprovalInFlightId;
        int taskNonQaId;
        int revQaAssignedId;
        int taskQaAssignedId;
        int revFutureId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var config = CreateConfigService(arrangeDb);
            var code = $"QAE_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            var docType = await config.CreateDocumentTypeAsync(
                new CreateDocumentTypeRequest(code, $"QA Transition Type {code}", 24, RequiresQaApprover: false),
                _fixture.SeededUserId);
            docTypeId = docType.Id;

            nonQaApproverId = await CreateUserAsync(arrangeDb, RoleType.Reviewer, "rev_inflight");
            (_, _, qaUserId, _, revApprovalInFlightId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb, docTypeId: docTypeId);
            (_, _, _, _, revQaAssignedId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb, docTypeId: docTypeId);
            (_, _, _, _, revFutureId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb, docTypeId: docTypeId);

            var approvalService = CreateApprovalService(arrangeDb);

            // 1. Route revision to non-QA reviewer while RequiresQaApprover is still false.
            var nonQaTask = await approvalService.CreateApprovalTaskAsync(
                revApprovalInFlightId,
                new CreateApprovalTaskRequest(nonQaApproverId),
                _fixture.SeededControllerUserId);
            taskNonQaId = nonQaTask.Id;

            // 2. Route another revision to QA approver, then later attempt SectionHead override.
            var qaTask = await approvalService.CreateApprovalTaskAsync(
                revQaAssignedId,
                new CreateApprovalTaskRequest(qaUserId),
                _fixture.SeededControllerUserId);
            taskQaAssignedId = qaTask.Id;

            // 3. Approve a third revision as FutureEffective via non-QA reviewer while flag is false.
            var futureTask = await approvalService.CreateApprovalTaskAsync(
                revFutureId,
                new CreateApprovalTaskRequest(nonQaApproverId),
                _fixture.SeededControllerUserId);
            await approvalService.ExecuteApprovalDecisionAsync(
                futureTask.Id,
                new ExecuteApprovalDecisionRequest(
                    DocumentApprovalDecision.Approve,
                    "Future effective before switch.",
                    EffectiveDate: FixedNow.UtcDateTime.AddDays(5),
                    Password: TestPassword),
                nonQaApproverId);

            // Switch RequiresQaApprover ON.
            await config.UpdateDocumentTypeAsync(
                docTypeId,
                new UpdateDocumentTypeRequest(docType.Name, docType.DefaultReviewCycleMonths, true, RequiresQaApprover: true),
                _fixture.SeededUserId);
        }

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var approvalService = CreateApprovalService(actDb);

            // Reviewer cannot make the in-flight revision effective after switch.
            var ex1 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                approvalService.ExecuteApprovalDecisionAsync(
                    taskNonQaId,
                    new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "Reviewer approve after switch.", Password: TestPassword),
                    nonQaApproverId));
            Assert.Equal(ExpectedQaRequiredMessage, ex1.Message);

            // SectionHead cannot override a QA-assigned task to make it effective without QA approving.
            var ex2 = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                approvalService.ExecuteApprovalDecisionAsync(
                    taskQaAssignedId,
                    new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Approve, "SectionHead override.", Password: TestPassword),
                    _fixture.SeededControllerUserId));
            Assert.Equal(ExpectedQaRequiredMessage, ex2.Message);
        }

        // Scheduled activation of the FutureEffective revision that lacked QA approval must also refuse to make it Effective.
        var afterEffectiveDate = new FixedTimeProvider(FixedNow.AddDays(6));
        await using (var actWorkerDb = _fixture.CreateDbContext(afterEffectiveDate))
        {
            var seq = new DatabaseSequenceHelper(actWorkerDb);
            var audit = new AuditEventService(actWorkerDb, seq, afterEffectiveDate);
            var worker = new DocumentEffectiveDateService(actWorkerDb, audit, NullLogger<DocumentEffectiveDateService>.Instance, null, afterEffectiveDate);

            var result = await worker.ProcessMaturedRevisionsAsync(afterEffectiveDate.GetUtcNow().UtcDateTime);
            var failure = Assert.Single(result.Failures, f => f.DocumentRevisionId == revFutureId);
            Assert.Equal(ExpectedQaRequiredMessage, failure.ErrorMessage);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var revInFlight = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revApprovalInFlightId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revInFlight.RevisionStatus);

        var revQa = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revQaAssignedId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revQa.RevisionStatus);

        var revFuture = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revFutureId);
        Assert.Equal(DocumentRevisionStatus.FutureEffective, revFuture.RevisionStatus);
    }

    [PostgresFact]
    public async Task Type_requiring_qa_becomes_effective_after_qa_approval()
    {
        int docTypeId;
        int qaUserId;
        int masterId;
        int revisionId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var config = CreateConfigService(arrangeDb);
            var code = $"QAO_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            var docType = await config.CreateDocumentTypeAsync(
                new CreateDocumentTypeRequest(code, $"QA Effective Type {code}", 24, RequiresQaApprover: true),
                _fixture.SeededUserId);
            docTypeId = docType.Id;

            (_, _, qaUserId, masterId, revisionId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb, docTypeId: docTypeId);
        }

        int taskId;
        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var approvalService = CreateApprovalService(actDb);
            var task = await approvalService.CreateApprovalTaskAsync(
                revisionId,
                new CreateApprovalTaskRequest(qaUserId),
                _fixture.SeededControllerUserId);
            taskId = task.Id;

            var decided = await approvalService.ExecuteApprovalDecisionAsync(
                taskId,
                new ExecuteApprovalDecisionRequest(
                    DocumentApprovalDecision.Approve,
                    "QA approval completed.",
                    Password: TestPassword),
                qaUserId);
            Assert.Equal(DocumentApprovalTaskStatus.Approved, decided.Status);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var revision = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revisionId);
        Assert.Equal(DocumentRevisionStatus.Effective, revision.RevisionStatus);

        var master = await assertDb.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == masterId);
        Assert.Equal(revisionId, master.CurrentEffectiveRevisionId);

        var sig = await assertDb.ElectronicSignatures.AsNoTracking().SingleAsync(s =>
            s.EntityType == "DocumentRevision" && s.EntityId == revisionId);
        Assert.Equal(qaUserId, sig.UserId);
        Assert.Equal(SignatureMeaning.Approved, sig.MeaningOfSignature);
    }

    [PostgresFact]
    public async Task Setting_change_is_audited()
    {
        int docTypeId;
        string typeName;
        int reviewCycle = 24;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var config = CreateConfigService(arrangeDb);
            var code = $"AUD_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            typeName = $"Audited Type {code}";
            var created = await config.CreateDocumentTypeAsync(
                new CreateDocumentTypeRequest(code, typeName, reviewCycle, RequiresQaApprover: false),
                _fixture.SeededUserId);
            docTypeId = created.Id;
        }

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var config = CreateConfigService(actDb);
            var updated = await config.UpdateDocumentTypeAsync(
                docTypeId,
                new UpdateDocumentTypeRequest(typeName, reviewCycle, IsActive: true, RequiresQaApprover: true),
                _fixture.SeededUserId);
            Assert.True(updated.RequiresQaApprover);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var docType = await assertDb.DocumentTypes.AsNoTracking().SingleAsync(t => t.Id == docTypeId);
        Assert.True(docType.RequiresQaApprover);

        var autoAudit = await assertDb.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(a =>
                a.EntityName == nameof(DocumentType) &&
                a.EntityId == docTypeId.ToString() &&
                a.Action == "Update");
        Assert.NotNull(autoAudit);
        Assert.Equal(_fixture.SeededUserId, autoAudit.UserId);
        Assert.Contains("\"RequiresQaApprover\":false", autoAudit.PreviousValue);
        Assert.Contains("\"RequiresQaApprover\":true", autoAudit.NewValue);

        var semanticAudit = await assertDb.AuditLogs.AsNoTracking()
            .Include(a => a.Changes)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(a =>
                a.ActionCode == "DocumentTypeUpdated" &&
                a.EntityId == docTypeId.ToString());
        Assert.NotNull(semanticAudit);
        Assert.Equal(_fixture.SeededUserId, semanticAudit.UserId);
        Assert.Contains(semanticAudit.Changes, c =>
            c.FieldName == "RequiresQaApprover" &&
            c.PreviousValue == "False" &&
            c.NewValue == "True");
    }

    [PostgresFact]
    public async Task Switching_the_setting_on_does_not_change_already_effective_revisions()
    {
        int docTypeId;
        string typeName;
        int masterId;
        int revisionId;

        await using (var arrangeDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var config = CreateConfigService(arrangeDb);
            var code = $"HIS_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
            typeName = $"Historical Effective Type {code}";
            var createdDocType = await config.CreateDocumentTypeAsync(
                new CreateDocumentTypeRequest(code, typeName, 24, RequiresQaApprover: false),
                _fixture.SeededUserId);
            docTypeId = createdDocType.Id;

            var reviewerApproverId = await CreateUserAsync(arrangeDb, RoleType.Reviewer, "hist_rev");
            (_, _, _, masterId, revisionId) = await SeedAwaitingApprovalRevisionAsync(arrangeDb, docTypeId: docTypeId);

            var approvalService = CreateApprovalService(arrangeDb);
            var task = await approvalService.CreateApprovalTaskAsync(
                revisionId,
                new CreateApprovalTaskRequest(reviewerApproverId),
                _fixture.SeededControllerUserId);
            await approvalService.ExecuteApprovalDecisionAsync(
                task.Id,
                new ExecuteApprovalDecisionRequest(
                    DocumentApprovalDecision.Approve,
                    "Approved before QA requirement switched on.",
                    Password: TestPassword),
                reviewerApproverId);
        }

        await using (var actDb = _fixture.CreateDbContext(new FixedTimeProvider(FixedNow)))
        {
            var config = CreateConfigService(actDb);
            var updated = await config.UpdateDocumentTypeAsync(
                docTypeId,
                new UpdateDocumentTypeRequest(typeName, 24, IsActive: true, RequiresQaApprover: true),
                _fixture.SeededUserId);
            Assert.True(updated.RequiresQaApprover);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var docType = await assertDb.DocumentTypes.AsNoTracking().SingleAsync(t => t.Id == docTypeId);
        Assert.True(docType.RequiresQaApprover);

        var revision = await assertDb.DocumentRevisions.AsNoTracking().SingleAsync(r => r.Id == revisionId);
        Assert.Equal(DocumentRevisionStatus.Effective, revision.RevisionStatus);

        var master = await assertDb.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == masterId);
        Assert.Equal(revisionId, master.CurrentEffectiveRevisionId);
    }
}
