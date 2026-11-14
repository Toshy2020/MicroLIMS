using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class NotificationPublisherPostgresTests
{
    private readonly PostgresTestFixture _fixture;
    private const string SigningPassword = "Notif-Signer-123!";

    public NotificationPublisherPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 30)];

    private static async Task<(Role ReviewerRole, Role SectionHeadRole, Role AnalystRole)> LoadRolesAsync(MicroLimsDbContext db)
    {
        var roles = await db.Roles.AsNoTracking().ToListAsync();
        return (
            roles.First(r => r.Type == RoleType.Reviewer),
            roles.First(r => r.Type == RoleType.SectionHead),
            roles.First(r => r.Type == RoleType.Analyst));
    }

    private async Task<DocumentSection> CreateSectionAsync(MicroLimsDbContext db, string tag)
    {
        var section = new DocumentSection
        {
            DepartmentId = _fixture.SeededDepartmentId,
            Code = UniqueCode($"SEC{tag}_"),
            Name = $"Notif Section {tag} {Guid.NewGuid():N}"[..36],
            IsActive = true
        };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    private async Task<User> CreateUserInSectionAsync(
        MicroLimsDbContext db,
        int roleId,
        int sectionId,
        string prefix,
        string? email = null)
    {
        var user = new User
        {
            FullName = $"{prefix} {Guid.NewGuid():N}"[..30],
            Username = UniqueCode($"{prefix}_").ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash(SigningPassword),
            RoleId = roleId,
            Email = email,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = sectionId
        });
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<(Sample Sample, TestOrder Order)> SeedReadyCountSampleAsync(
        MicroLimsDbContext db,
        int sectionId,
        int performerId,
        string? recordedDisplay = null,
        decimal? recordedNumeric = null)
    {
        var testCode = UniqueCode("CNT_");
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = testCode,
            DisplayName = "Notification Test Count",
            WorkflowType = WorkflowType.CountTest,
            SectionId = sectionId
        });

        var cause = new CauseOfTesting { Name = UniqueCode("Routine_") };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            Category = SampleCategory.FinishedProduct,
            ControlNumber = UniqueCode("CTRL-"),
            ReferenceNumber = UniqueCode("REF-"),
            CauseOfTestingId = cause.Id,
            ReceivedByUserId = _fixture.SeededUserId,
            Status = SampleStatus.InTesting
        };
        var order = new TestOrder
        {
            TestCode = testCode,
            Status = ApprovalStatus.ResultEntered,
            CurrentStep = WorkflowStep.Ready,
            SectionId = sectionId,
            AssignedAnalystId = performerId
        };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        if (recordedDisplay is not null || recordedNumeric is not null)
        {
            db.CountTestReadings.Add(new CountTestReading
            {
                TestOrderId = order.Id,
                StepName = "Count",
                IsActive = true,
                CalculatedResult = recordedNumeric,
                ReportedResult = recordedDisplay ?? recordedNumeric?.ToString() ?? "0",
                Status = ResultStatus.WithinLimits,
                EnteredByUserId = performerId
            });
            db.Results.Add(new Result
            {
                TestOrderId = order.Id,
                RawValue = recordedDisplay ?? recordedNumeric?.ToString() ?? "0",
                InterpretedValue = recordedDisplay ?? "0",
                Type = ResultType.Numeric,
                EnteredByUserId = performerId
            });
            await db.SaveChangesAsync();
        }

        return (sample, order);
    }

    [PostgresFact]
    public async Task SubmitForReview_NotifiesReviewersOfThatSection_NotOtherSections_NotThePerformer()
    {
        int sectionAId, sectionBId, performerReviewerId, eligibleReviewerId, otherSectionReviewerId, sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (reviewerRole, _, _) = await LoadRolesAsync(arrangeDb);
            var sectionA = await CreateSectionAsync(arrangeDb, "A");
            var sectionB = await CreateSectionAsync(arrangeDb, "B");
            sectionAId = sectionA.Id;
            sectionBId = sectionB.Id;

            // Holds Samples.Review AND is in section A, but performed the test -> excluded by SoD.
            var performer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionAId, "perf", "perf@lab.test");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionAId, "revA", "revA@lab.test");
            var otherReviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionBId, "revB", "revB@lab.test");

            performerReviewerId = performer.Id;
            eligibleReviewerId = reviewer.Id;
            otherSectionReviewerId = otherReviewer.Id;

            var (sample, _) = await SeedReadyCountSampleAsync(arrangeDb, sectionAId, performerReviewerId);
            sampleId = sample.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var reviewService = TestServiceFactory.SampleReview(actDb);
            await reviewService.AutoSubmitForReviewIfReadyAsync(sampleId, performerReviewerId);
            await actDb.SaveChangesAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var dedupeKey = $"ReviewWaiting:sample:{sampleId}:section:{sectionAId}";
        var rows = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == dedupeKey)
            .ToListAsync();

        var only = Assert.Single(rows);
        Assert.Equal(eligibleReviewerId, only.UserId);
        Assert.Equal("ReviewWaiting", only.Type);
        Assert.Equal("Sample awaiting review", only.Title);
        Assert.Null(only.ResolvedAt);
        Assert.DoesNotContain(rows, r => r.UserId == performerReviewerId);
        Assert.DoesNotContain(rows, r => r.UserId == otherSectionReviewerId);
    }

    [PostgresFact]
    public async Task SubmitForReview_Twice_OneNotificationPerReviewer()
    {
        int sectionId, performerId, reviewerId, sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (reviewerRole, _, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "D1");
            sectionId = section.Id;

            var performer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "an");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionId, "rev");
            performerId = performer.Id;
            reviewerId = reviewer.Id;

            var (sample, _) = await SeedReadyCountSampleAsync(arrangeDb, sectionId, performerId);
            sampleId = sample.Id;
        }

        await using (var actDb1 = _fixture.CreateDbContext())
        {
            var svc = TestServiceFactory.SampleReview(actDb1);
            await svc.AutoSubmitForReviewIfReadyAsync(sampleId, performerId);
            await actDb1.SaveChangesAsync();
        }

        // Reset section status in testing without resolving so a second auto-submit runs against an open row.
        await using (var resetDb = _fixture.CreateDbContext())
        {
            var signoff = await resetDb.SampleSectionSignoffs
                .FirstAsync(s => s.SampleId == sampleId && s.SectionId == sectionId);
            signoff.Status = SectionSignoffStatus.InTesting;
            var sample = await resetDb.Samples.FirstAsync(s => s.Id == sampleId);
            sample.Status = SampleStatus.InTesting;
            await resetDb.SaveChangesAsync();
        }

        await using (var actDb2 = _fixture.CreateDbContext())
        {
            var svc = TestServiceFactory.SampleReview(actDb2);
            await svc.AutoSubmitForReviewIfReadyAsync(sampleId, performerId);
            await actDb2.SaveChangesAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var dedupeKey = $"ReviewWaiting:sample:{sampleId}:section:{sectionId}";
        var rows = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == dedupeKey && n.UserId == reviewerId)
            .ToListAsync();

        Assert.Single(rows);
    }

    [PostgresFact]
    public async Task CompleteReview_ResolvesReviewWaiting_AndNotifiesApprovers()
    {
        int sectionId, performerId, reviewerId, approverId, sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (reviewerRole, headRole, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "CR");
            sectionId = section.Id;

            var performer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "perf");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionId, "rev");
            var approver = await CreateUserInSectionAsync(arrangeDb, headRole.Id, sectionId, "app");

            performerId = performer.Id;
            reviewerId = reviewer.Id;
            approverId = approver.Id;

            var (sample, _) = await SeedReadyCountSampleAsync(arrangeDb, sectionId, performerId);
            sampleId = sample.Id;

            var reviewSvc = TestServiceFactory.SampleReview(arrangeDb);
            await reviewSvc.AutoSubmitForReviewIfReadyAsync(sampleId, performerId);
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var reviewSvc = TestServiceFactory.SampleReview(actDb);
            await reviewSvc.CompleteReviewAsync(sampleId, reviewerId, SigningPassword, "Verified", "127.0.0.1", sectionId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var reviewKey = $"ReviewWaiting:sample:{sampleId}:section:{sectionId}";
        var approvalKey = $"ApprovalWaiting:sample:{sampleId}:section:{sectionId}";

        var reviewRow = await assertDb.NotificationLogs.AsNoTracking()
            .SingleAsync(n => n.DedupeKey == reviewKey && n.UserId == reviewerId);
        Assert.NotNull(reviewRow.ResolvedAt);

        var approvalRows = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == approvalKey)
            .ToListAsync();
        Assert.Contains(approvalRows, r => r.UserId == approverId && r.Type == "ApprovalWaiting" && r.ResolvedAt == null);
        Assert.DoesNotContain(approvalRows, r => r.UserId == performerId);
        Assert.DoesNotContain(approvalRows, r => r.UserId == reviewerId);
    }

    [PostgresFact]
    public async Task ReturnTest_NotifiesAssignedAnalyst_WithReasonNotInEmail()
    {
        const string returnReason = "Recheck serial dilution step 2";
        int sectionId, analystId, reviewerId, orderId, sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (reviewerRole, _, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "RT");
            sectionId = section.Id;

            var analyst = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "retan", "analyst@lab.test");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionId, "retrev", "rev@lab.test");
            analystId = analyst.Id;
            reviewerId = reviewer.Id;

            var (sample, order) = await SeedReadyCountSampleAsync(arrangeDb, sectionId, analystId);
            sampleId = sample.Id;
            orderId = order.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var reviewSvc = TestServiceFactory.SampleReview(actDb);
            await reviewSvc.ReturnToAnalystAsync(orderId, reviewerId, returnReason);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var dedupeKey = $"TestReturned:test:{orderId}";
        var notif = await assertDb.NotificationLogs.AsNoTracking()
            .SingleAsync(n => n.DedupeKey == dedupeKey && n.UserId == analystId);

        Assert.Equal("TestReturned", notif.Type);
        Assert.Contains(returnReason, notif.Message);
        Assert.Null(notif.ResolvedAt);

        var outboxForNotif = await assertDb.EmailOutbox.AsNoTracking()
            .Where(e => e.NotificationLogId == notif.Id || (e.UserId == analystId && e.Body.Contains(returnReason)))
            .ToListAsync();
        Assert.Empty(outboxForNotif);
    }

    [PostgresFact]
    public async Task Decide_ResolvesApprovalWaiting()
    {
        int sectionId, performerId, reviewerId, approverId, sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (reviewerRole, headRole, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "DEC");
            sectionId = section.Id;

            var performer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "dperf");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionId, "drev");
            var approver = await CreateUserInSectionAsync(arrangeDb, headRole.Id, sectionId, "dapp");

            performerId = performer.Id;
            reviewerId = reviewer.Id;
            approverId = approver.Id;

            var (sample, _) = await SeedReadyCountSampleAsync(arrangeDb, sectionId, performerId);
            sampleId = sample.Id;

            var reviewSvc = TestServiceFactory.SampleReview(arrangeDb);
            await reviewSvc.AutoSubmitForReviewIfReadyAsync(sampleId, performerId);
            await arrangeDb.SaveChangesAsync();
            await reviewSvc.CompleteReviewAsync(sampleId, reviewerId, SigningPassword, "Reviewed", "127.0.0.1", sectionId);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var approvalSvc = TestServiceFactory.SampleApproval(actDb);
            await approvalSvc.DecideAsync(sampleId, approverId, SigningPassword, ApprovalDecision.Approve, "Approved", "127.0.0.1", sectionId: sectionId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var approvalKey = $"ApprovalWaiting:sample:{sampleId}:section:{sectionId}";
        var rows = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == approvalKey)
            .ToListAsync();

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.NotNull(r.ResolvedAt));
    }

    [PostgresFact]
    public async Task AssignAnalyst_NotifiesAssignee_NotWhenAssigningToSelf()
    {
        int sectionId, actingHeadId, targetAnalystId, sampleId, order1Id, order2Id;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (_, headRole, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "ASN");
            sectionId = section.Id;

            var actingHead = await CreateUserInSectionAsync(arrangeDb, headRole.Id, sectionId, "head");
            var analyst = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "analyst");
            actingHeadId = actingHead.Id;
            targetAnalystId = analyst.Id;

            var cause = new CauseOfTesting { Name = UniqueCode("Routine_") };
            arrangeDb.CausesOfTesting.Add(cause);
            await arrangeDb.SaveChangesAsync();

            var sample = new Sample
            {
                Category = SampleCategory.FinishedProduct,
                ControlNumber = UniqueCode("CTRL-"),
                ReferenceNumber = UniqueCode("REF-"),
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                Status = SampleStatus.Received
            };
            var o1 = new TestOrder { TestCode = "T1", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = sectionId };
            var o2 = new TestOrder { TestCode = "T2", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = sectionId };
            sample.TestOrders.Add(o1);
            sample.TestOrders.Add(o2);
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            sampleId = sample.Id;
            order1Id = o1.Id;
            order2Id = o2.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var scope = new UserSectionScopeService(actDb);
            var svc = new SampleAssignmentService(actDb, TestServiceFactory.TestingWorkspace(actDb, scope), scope);
            await svc.AssignTestsAsync(sampleId, new[]
            {
                new TestAnalystAssignment(order1Id, targetAnalystId),
                new TestAnalystAssignment(order2Id, actingHeadId)
            }, actingHeadId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var otherKey = $"TestAssigned:test:{order1Id}:user:{targetAnalystId}";
        var selfKey = $"TestAssigned:test:{order2Id}:user:{actingHeadId}";

        var assignedNotif = await assertDb.NotificationLogs.AsNoTracking()
            .SingleAsync(n => n.DedupeKey == otherKey && n.UserId == targetAnalystId);
        Assert.Equal("TestAssigned", assignedNotif.Type);

        var selfNotifs = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == selfKey)
            .ToListAsync();
        Assert.Empty(selfNotifs);
    }

    [PostgresFact]
    public async Task DocumentReviewTask_ApprovalTask_TrainingAssignment_EachNotifyTheirUser()
    {
        int authorId, reviewerId, approverId, traineeId, revisionId, reviewTaskId, approvalTaskId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (reviewerRole, headRole, analystRole) = await LoadRolesAsync(arrangeDb);
            var author = await CreateUserInSectionAsync(arrangeDb, headRole.Id, _fixture.SeededSectionId, "docauth");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, _fixture.SeededSectionId, "docrev");
            var approver = await CreateUserInSectionAsync(arrangeDb, headRole.Id, _fixture.SeededSectionId, "docapp");
            var trainee = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, _fixture.SeededSectionId, "doctrn");

            authorId = author.Id;
            reviewerId = reviewer.Id;
            approverId = approver.Id;
            traineeId = trainee.Id;

            var seqHelper = _fixture.CreateSequenceHelper(arrangeDb);
            var audit = new AuditEventService(arrangeDb, seqHelper);
            var auth = new DocumentAuthorizationService(arrangeDb);
            var masterService = new DocumentMasterService(arrangeDb, seqHelper, audit, auth);
            var fileService = new DocumentFileService(arrangeDb, new InMemoryFileStorageService(), audit, auth);

            var masterDto = await masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
                CompanyDocumentCode: UniqueCode("DOC-N-"),
                Title: "Notification Integration SOP",
                DocumentTypeId: _fixture.SeededDocTypeId,
                DepartmentId: _fixture.SeededDepartmentId,
                SectionId: _fixture.SeededSectionId,
                DocumentOwnerUserId: authorId,
                Confidentiality: DocumentConfidentiality.Internal,
                Category: "SOP",
                Keywords: new List<string> { "Notif" },
                InitialRevisionNumber: "01",
                ReviewCycleMonths: 12), authorId);
            revisionId = masterDto.Revisions[0].Id;

            await fileService.UploadRevisionFileAsync(
                revisionId, FileRole.ControlledPdf, "doc.pdf", "application/pdf",
                Encoding.UTF8.GetBytes("%PDF-1.4 Test"), authorId);
        }

        await using (var actReviewDb = _fixture.CreateDbContext())
        {
            var audit = new AuditEventService(actReviewDb, _fixture.CreateSequenceHelper(actReviewDb));
            var auth = new DocumentAuthorizationService(actReviewDb);
            var reviewSvc = new DocumentReviewService(actReviewDb, audit, auth);
            var reviewTask = await reviewSvc.SubmitForReviewAsync(revisionId, new SubmitForReviewRequest
            {
                ReviewerUserId = reviewerId,
                SubmissionNotes = "Check SOP"
            }, authorId);
            reviewTaskId = reviewTask.Id;
        }

        await using (var assertReviewDb = _fixture.CreateDbContext())
        {
            var reviewNotif = await assertReviewDb.NotificationLogs.AsNoTracking()
                .SingleAsync(n => n.DedupeKey == $"DocumentTask:review:{reviewTaskId}" && n.UserId == reviewerId);
            Assert.Equal("DocumentTask", reviewNotif.Type);
            Assert.Null(reviewNotif.ResolvedAt);
        }

        await using (var actCompleteAndApproveDb = _fixture.CreateDbContext())
        {
            var audit = new AuditEventService(actCompleteAndApproveDb, _fixture.CreateSequenceHelper(actCompleteAndApproveDb));
            var auth = new DocumentAuthorizationService(actCompleteAndApproveDb);
            var reviewSvc = new DocumentReviewService(actCompleteAndApproveDb, audit, auth);
            await reviewSvc.DecideReviewAsync(reviewTaskId, new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview,
                ReviewNotes = "OK"
            }, reviewerId);

            var approvalSvc = new DocumentApprovalService(actCompleteAndApproveDb, audit, auth, new ElectronicSignatureService(actCompleteAndApproveDb));
            var appTask = await approvalSvc.CreateApprovalTaskAsync(revisionId, new CreateApprovalTaskRequest(approverId), authorId);
            approvalTaskId = appTask.Id;
        }

        await using (var assertApprovalDb = _fixture.CreateDbContext())
        {
            var completedReviewNotif = await assertApprovalDb.NotificationLogs.AsNoTracking()
                .SingleAsync(n => n.DedupeKey == $"DocumentTask:review:{reviewTaskId}");
            Assert.NotNull(completedReviewNotif.ResolvedAt);

            var approvalNotif = await assertApprovalDb.NotificationLogs.AsNoTracking()
                .SingleAsync(n => n.DedupeKey == $"DocumentTask:approval:{approvalTaskId}" && n.UserId == approverId);
            Assert.Equal("DocumentTask", approvalNotif.Type);
            Assert.Null(approvalNotif.ResolvedAt);
        }

        List<int> assignmentIds;
        await using (var actApproveAndTrainDb = _fixture.CreateDbContext())
        {
            var audit = new AuditEventService(actApproveAndTrainDb, _fixture.CreateSequenceHelper(actApproveAndTrainDb));
            var auth = new DocumentAuthorizationService(actApproveAndTrainDb);
            var approvalSvc = new DocumentApprovalService(actApproveAndTrainDb, audit, auth, new ElectronicSignatureService(actApproveAndTrainDb));
            await approvalSvc.ExecuteApprovalDecisionAsync(approvalTaskId, new ExecuteApprovalDecisionRequest(
                Decision: DocumentApprovalDecision.Approve,
                DecisionNotes: "Effective",
                EffectiveDate: DateTime.UtcNow.AddMinutes(-1),
                Password: SigningPassword), approverId, "127.0.0.1");

            var trainingSvc = new TrainingAssignmentService(
                actApproveAndTrainDb,
                audit,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<TrainingAssignmentService>.Instance);
            var trainingRes = await trainingSvc.AssignToUsersAsync(
                new ManualTrainingAssignmentRequest(revisionId, new List<int> { traineeId }),
                authorId);
            assignmentIds = trainingRes.CreatedAssignmentIds;
        }

        await using var assertFinalDb = _fixture.CreateDbContext();
        var resolvedApproval = await assertFinalDb.NotificationLogs.AsNoTracking()
            .SingleAsync(n => n.DedupeKey == $"DocumentTask:approval:{approvalTaskId}");
        Assert.NotNull(resolvedApproval.ResolvedAt);

        var assignmentId = Assert.Single(assignmentIds);
        var trainingNotif = await assertFinalDb.NotificationLogs.AsNoTracking()
            .SingleAsync(n => n.DedupeKey == $"TrainingAssigned:{assignmentId}" && n.UserId == traineeId);
        Assert.Equal("TrainingAssigned", trainingNotif.Type);
        Assert.Null(trainingNotif.ResolvedAt);
    }

    [PostgresFact]
    public async Task CausingTransactionRollsBack_NoNotificationRows_NoOutboxRows()
    {
        int sectionId, userId;
        var dedupeKey = $"ItemOverdue:test:{Guid.NewGuid():N}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (_, _, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "RB");
            sectionId = section.Id;
            var user = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "rbuser", "rb@lab.test");
            userId = user.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(actDb);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await UnitOfWork.RunAsync(actDb, async () =>
                {
                    await publisher.PublishAsync(new NotificationRequest(
                        Type: "ItemOverdue",
                        UserIds: new[] { userId },
                        Message: "Rollback test item is overdue.",
                        TargetType: "test-order",
                        TargetId: 999,
                        SectionId: sectionId,
                        DedupeKey: dedupeKey));
                    await actDb.SaveChangesAsync();
                    throw new InvalidOperationException("Simulated failure after SaveChanges");
                });
            });
        }

        await using var assertDb = _fixture.CreateDbContext();
        var notifs = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == dedupeKey)
            .ToListAsync();
        var outbox = await assertDb.EmailOutbox.AsNoTracking()
            .Where(e => e.UserId == userId && e.Body.Contains("Rollback test item"))
            .ToListAsync();

        Assert.Empty(notifs);
        Assert.Empty(outbox);
    }

    [PostgresFact]
    public async Task MessageDoesNotContainResultValue()
    {
        const decimal secretResult = 948271m;
        const string secretText = "948271 CFU/mL";
        int sectionId, performerId, reviewerId, approverId, sampleId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (reviewerRole, headRole, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "SEC");
            sectionId = section.Id;

            var performer = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "sperf", "sperf@lab.test");
            var reviewer = await CreateUserInSectionAsync(arrangeDb, reviewerRole.Id, sectionId, "srev", "srev@lab.test");
            var approver = await CreateUserInSectionAsync(arrangeDb, headRole.Id, sectionId, "sapp", "sapp@lab.test");
            performerId = performer.Id;
            reviewerId = reviewer.Id;
            approverId = approver.Id;

            var (sample, _) = await SeedReadyCountSampleAsync(arrangeDb, sectionId, performerId, secretText, secretResult);
            sampleId = sample.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var reviewSvc = TestServiceFactory.SampleReview(actDb);
            await reviewSvc.AutoSubmitForReviewIfReadyAsync(sampleId, performerId);
            await actDb.SaveChangesAsync();
            await reviewSvc.CompleteReviewAsync(sampleId, reviewerId, SigningPassword, "Done", "127.0.0.1", sectionId);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var notifs = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.SampleId == sampleId)
            .ToListAsync();
        var notifIds = notifs.Select(n => (int?)n.Id).ToList();
        var outbox = await assertDb.EmailOutbox.AsNoTracking()
            .Where(e => notifIds.Contains(e.NotificationLogId))
            .ToListAsync();

        Assert.NotEmpty(notifs);
        Assert.All(notifs, n =>
        {
            Assert.DoesNotContain("948271", n.Message);
            Assert.DoesNotContain("CFU/mL", n.Message);
            Assert.DoesNotContain("WithinLimits", n.Message);
        });
        Assert.All(outbox, e =>
        {
            Assert.DoesNotContain("948271", e.Subject);
            Assert.DoesNotContain("948271", e.Body);
        });
    }

    [PostgresFact]
    public async Task UnknownType_Throws()
    {
        await using var actDb = _fixture.CreateDbContext();
        var publisher = new NotificationPublisher(actDb);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.PublishAsync(new NotificationRequest(
                Type: "CompletelyUnknownNotificationType",
                UserIds: new[] { _fixture.SeededUserId },
                Message: "Should throw",
                TargetType: null,
                TargetId: null,
                SectionId: null,
                DedupeKey: null)));
    }

    [PostgresFact]
    public async Task EmailAllowedType_ImmediateDefault_AddsOutboxRow_WhenUserHasEmail()
    {
        int sectionId, userWithEmailId, userWithoutEmailId;
        var dedupeKey = $"ItemOverdue:multi:{Guid.NewGuid():N}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (_, _, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "EML");
            sectionId = section.Id;

            var withEmail = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "wemail", "has@lab.test");
            var withoutEmail = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "noemail", null);
            userWithEmailId = withEmail.Id;
            userWithoutEmailId = withoutEmail.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(actDb);
            await publisher.PublishAsync(new NotificationRequest(
                Type: "ItemOverdue",
                UserIds: new[] { userWithEmailId, userWithoutEmailId },
                Message: "Item X is overdue.",
                TargetType: "test-order",
                TargetId: 10,
                SectionId: sectionId,
                DedupeKey: dedupeKey));
            await actDb.SaveChangesAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var logs = await assertDb.NotificationLogs.AsNoTracking()
            .Where(n => n.DedupeKey == dedupeKey)
            .ToListAsync();
        Assert.Equal(2, logs.Count);

        var logIds = logs.Select(l => (int?)l.Id).ToList();
        var outboxRows = await assertDb.EmailOutbox.AsNoTracking()
            .Where(e => logIds.Contains(e.NotificationLogId))
            .ToListAsync();

        var onlyOutbox = Assert.Single(outboxRows);
        Assert.Equal(userWithEmailId, onlyOutbox.UserId);
        Assert.Equal("has@lab.test", onlyOutbox.ToAddress);
        Assert.Equal(EmailOutboxKind.Immediate, onlyOutbox.Kind);
        Assert.Equal("Item overdue", onlyOutbox.Subject);
        Assert.Contains("Item X is overdue.", onlyOutbox.Body);
    }

    [PostgresFact]
    public async Task EmailAllowedType_UserChoseOff_NoOutboxRow_UnlessMandatoryForSection()
    {
        int sectionId, userId;
        var keyOff = $"CalibrationDue:off:{Guid.NewGuid():N}";
        var keyMandatory = $"CalibrationDue:mand:{Guid.NewGuid():N}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var (_, _, analystRole) = await LoadRolesAsync(arrangeDb);
            var section = await CreateSectionAsync(arrangeDb, "OFF");
            sectionId = section.Id;

            var user = await CreateUserInSectionAsync(arrangeDb, analystRole.Id, sectionId, "offuser", "offuser@lab.test");
            userId = user.Id;

            arrangeDb.UserNotificationPreferences.Add(new UserNotificationPreference
            {
                UserId = userId,
                Type = "CalibrationDue",
                EmailMode = EmailMode.Off
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actOffDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(actOffDb);
            await publisher.PublishAsync(new NotificationRequest(
                Type: "CalibrationDue",
                UserIds: new[] { userId },
                Message: "Balance calibration due.",
                TargetType: "equipment",
                TargetId: 5,
                SectionId: sectionId,
                DedupeKey: keyOff));
            await actOffDb.SaveChangesAsync();
        }

        await using (var assertOffDb = _fixture.CreateDbContext())
        {
            var offLog = await assertOffDb.NotificationLogs.AsNoTracking()
                .SingleAsync(n => n.DedupeKey == keyOff && n.UserId == userId);
            var outboxForOff = await assertOffDb.EmailOutbox.AsNoTracking()
                .Where(e => e.NotificationLogId == offLog.Id)
                .ToListAsync();
            Assert.Empty(outboxForOff);
        }

        await using (var arrangeMandDb = _fixture.CreateDbContext())
        {
            arrangeMandDb.MandatoryNotificationTypes.Add(new MandatoryNotificationType
            {
                SectionId = sectionId,
                Type = "CalibrationDue",
                SetByUserId = _fixture.SeededUserId
            });
            await arrangeMandDb.SaveChangesAsync();
        }

        await using (var actMandDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(actMandDb);
            await publisher.PublishAsync(new NotificationRequest(
                Type: "CalibrationDue",
                UserIds: new[] { userId },
                Message: "Balance calibration mandatory alert.",
                TargetType: "equipment",
                TargetId: 5,
                SectionId: sectionId,
                DedupeKey: keyMandatory));
            await actMandDb.SaveChangesAsync();
        }

        await using var assertMandDb = _fixture.CreateDbContext();
        var mandLog = await assertMandDb.NotificationLogs.AsNoTracking()
            .SingleAsync(n => n.DedupeKey == keyMandatory && n.UserId == userId);
        var outboxForMand = await assertMandDb.EmailOutbox.AsNoTracking()
            .Where(e => e.NotificationLogId == mandLog.Id)
            .ToListAsync();

        var onlyMandOutbox = Assert.Single(outboxForMand);
        Assert.Equal("offuser@lab.test", onlyMandOutbox.ToAddress);
        Assert.Equal(EmailOutboxKind.Immediate, onlyMandOutbox.Kind);
    }
}
