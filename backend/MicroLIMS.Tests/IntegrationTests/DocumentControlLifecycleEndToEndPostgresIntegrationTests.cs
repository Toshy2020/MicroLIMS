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
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

/// <summary>
/// Work Package 8 (WP8) Comprehensive End-to-End Lifecycle Integration Verification Suite.
/// Verifies that WP1–WP7 interact harmoniously against a live PostgreSQL container
/// executing the unbroken Document Control lifecycle.
/// </summary>
[Collection("PostgresDatabaseCollection")]
public class DocumentControlLifecycleEndToEndPostgresIntegrationTests
{
    private const string DefaultPassword = "Part11-Valid-Password-2026!";
    private readonly PostgresTestFixture _fixture;

    public DocumentControlLifecycleEndToEndPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private struct UserContext
    {
        public User Author;
        public User Reviewer;
        public User Approver;
        public User Admin;
    }

    private struct ServiceHarness
    {
        public DocumentMasterService MasterService;
        public DocumentFileService FileService;
        public DocumentReviewService ReviewService;
        public DocumentRevisionService RevisionService;
        public ElectronicSignatureService SignatureService;
        public DocumentApprovalService ApprovalService;
        public DocumentEffectiveDateService WorkerService;
        public PeriodicReviewService PeriodicReviewService;
        public AuditEventService AuditService;
    }

    private async Task<UserContext> SeedUsersAsync(MicroLimsDbContext db)
    {
        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst)
            ?? db.Roles.Add(new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true }).Entity;

        var reviewerRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Reviewer)
            ?? db.Roles.Add(new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsActive = true }).Entity;

        var sectionHeadRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.SectionHead)
            ?? db.Roles.Add(new Role { Type = RoleType.SectionHead, Name = "SectionHead", IsActive = true }).Entity;

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.SystemAdministrator)
            ?? db.Roles.Add(new Role { Type = RoleType.SystemAdministrator, Name = "SystemAdministrator", IsActive = true }).Entity;

        await db.SaveChangesAsync();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword);
        string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);

        var author = new User
        {
            FullName = $"Author User {suffix}",
            Username = $"auth_{suffix}",
            PasswordHash = passwordHash,
            RoleId = analystRole.Id,
            IsActive = true
        };

        var reviewer = new User
        {
            FullName = $"Reviewer {suffix}",
            Username = $"rev_{suffix}",
            PasswordHash = passwordHash,
            RoleId = reviewerRole.Id,
            IsActive = true
        };

        var approver = new User
        {
            FullName = $"Approver {suffix}",
            Username = $"app_{suffix}",
            PasswordHash = passwordHash,
            RoleId = sectionHeadRole.Id,
            IsActive = true
        };

        var admin = new User
        {
            FullName = $"Sys Admin {suffix}",
            Username = $"adm_{suffix}",
            PasswordHash = passwordHash,
            RoleId = adminRole.Id,
            IsActive = true
        };

        db.Users.AddRange(author, reviewer, approver, admin);
        await db.SaveChangesAsync();

        return new UserContext
        {
            Author = author,
            Reviewer = reviewer,
            Approver = approver,
            Admin = admin
        };
    }

    private ServiceHarness CreateHarness(MicroLimsDbContext db)
    {
        var seqHelper = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var storage = new InMemoryFileStorageService();

        var master = new DocumentMasterService(db, seqHelper, audit, auth);
        var file = new DocumentFileService(db, storage, audit, auth);
        var review = new DocumentReviewService(db, audit, auth);
        var revision = new DocumentRevisionService(db, audit, auth);
        var signature = new ElectronicSignatureService(db);
        var approval = new DocumentApprovalService(db, audit, auth, signature);
        var worker = new DocumentEffectiveDateService(db, audit, NullLogger<DocumentEffectiveDateService>.Instance);
        var periodicReview = new PeriodicReviewService(db, audit, auth, approval, NullLogger<PeriodicReviewService>.Instance);

        return new ServiceHarness
        {
            MasterService = master,
            FileService = file,
            ReviewService = review,
            RevisionService = revision,
            SignatureService = signature,
            ApprovalService = approval,
            WorkerService = worker,
            PeriodicReviewService = periodicReview,
            AuditService = audit
        };
    }

    [PostgresFact]
    public async Task WP8_EndToEnd_UnbrokenDocumentControlLifecycle_Postgres()
    {
        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 1: Create Document Master + Initial Revision 01 in Draft
        // -----------------------------------------------------------------------------------------
        await using var db = _fixture.CreateDbContext();
        var users = await SeedUsersAsync(db);
        var harness = CreateHarness(db);

        string docCode = $"SOP-E2E-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
        var createMasterReq = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: docCode,
            Title: "End-To-End Regulated SOP Integration Test",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: users.Author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Quality",
            Keywords: new List<string> { "WP8", "Integration", "E2E" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 12
        );

        var master = await harness.MasterService.RegisterDocumentMasterAsync(createMasterReq, users.Author.Id);
        Assert.NotNull(master);
        Assert.Equal(DocumentRecordStatus.Active, master.RecordStatus);

        var initialRevision = await db.DocumentRevisions.FirstAsync(r => r.DocumentMasterId == master.Id);
        Assert.Equal("01", initialRevision.RevisionNumber);
        Assert.Equal(DocumentRevisionStatus.Draft, initialRevision.RevisionStatus);

        // Upload Controlled PDF for Revision 01
        byte[] pdfBytes1 = Encoding.UTF8.GetBytes("%PDF-1.4 E2E Rev 01 Content");
        await harness.FileService.UploadRevisionFileAsync(
            initialRevision.Id,
            FileRole.ControlledPdf,
            "e2e_rev01.pdf",
            "application/pdf",
            pdfBytes1,
            users.Author.Id
        );

        // Advance Revision 01 directly to Effective so we can test the complete Revision Lifecycle
        initialRevision.RevisionStatus = DocumentRevisionStatus.Effective;
        initialRevision.EffectiveDate = DateTime.UtcNow.AddMonths(-6);
        initialRevision.ReviewCycleMonths = 12;
        initialRevision.NextReviewDate = DateTime.UtcNow.AddMonths(6);
        var masterEntity = await db.DocumentMasters.FindAsync(master.Id);
        masterEntity!.CurrentEffectiveRevisionId = initialRevision.Id;
        await db.SaveChangesAsync();

        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 2: Author creates Revision 02 from Effective Revision 01
        // -----------------------------------------------------------------------------------------
        var createRevReq = new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "System upgrade per CC-2026-001",
            ChangeSummary: "Major revision for new automated analytical system",
            ChangeReference: "CR-2026-E2E"
        );

        var rev2Dto = await harness.RevisionService.CreateRevisionFromEffectiveAsync(master.Id, createRevReq, users.Author.Id);
        Assert.Equal("02", rev2Dto.RevisionNumber);
        Assert.Equal(DocumentRevisionStatus.Draft, rev2Dto.RevisionStatus);
        Assert.Equal(RevisionType.Major, rev2Dto.RevisionType);

        // Upload new Controlled PDF for Revision 02
        byte[] pdfBytes2 = Encoding.UTF8.GetBytes("%PDF-1.4 E2E Rev 02 Content with new procedures");
        await harness.FileService.UploadRevisionFileAsync(
            rev2Dto.Id,
            FileRole.ControlledPdf,
            "e2e_rev02.pdf",
            "application/pdf",
            pdfBytes2,
            users.Author.Id
        );

        // Add Revision Change Items
        await harness.RevisionService.AddChangeItemAsync(rev2Dto.Id, new AddChangeItemRequest(
            SectionNumber: "4.1",
            SectionTitle: "HPLC Run Parameters",
            DescriptionOfChange: "Updated HPLC instrument run parameters",
            ChangeRationale: "Optimized chromatography parameters",
            ChangeCategory: "Modification",
            Status: "Draft"
        ), users.Author.Id);

        // Complete Impact Assessment for Revision 02
        await harness.RevisionService.SaveImpactAssessmentAsync(rev2Dto.Id, new SaveImpactAssessmentRequest(
            ProcedureOrMethodImpact: true,
            ProcedureOrMethodDetails: "Procedure steps updated for modern HPLC columns",
            TrainingImpact: false,
            TrainingDetails: null,
            FormsOrTemplatesImpact: false,
            FormsOrTemplatesDetails: null,
            SpecificationsImpact: false,
            SpecificationsDetails: null,
            EquipmentImpact: false,
            EquipmentDetails: null,
            MaterialsOrMediaImpact: false,
            MaterialsOrMediaDetails: null,
            ValidationImpact: false,
            ValidationDetails: null,
            RegulatoryCommitmentImpact: false,
            RegulatoryCommitmentDetails: null,
            RelatedDocumentsImpact: false,
            RelatedDocumentsDetails: null
        ), users.Author.Id);

        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 3: Technical Review Submission (WP1)
        // -----------------------------------------------------------------------------------------
        var reviewTaskDto = await harness.ReviewService.SubmitForReviewAsync(rev2Dto.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = users.Reviewer.Id,
            SubmissionNotes = "Please review section 4.1 HPLC parameters carefully."
        }, users.Author.Id);

        Assert.NotNull(reviewTaskDto);
        Assert.Equal(ReviewTaskStatus.Pending, reviewTaskDto.Status);

        var rev2InDb = await db.DocumentRevisions.FindAsync(rev2Dto.Id);
        Assert.Equal(DocumentRevisionStatus.InReview, rev2InDb!.RevisionStatus);

        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 4: Reviewer Records Finding & Author Responds
        // -----------------------------------------------------------------------------------------
        var findingDto = await harness.ReviewService.AddReviewFindingAsync(reviewTaskDto.Id, new AddReviewFindingRequest
        {
            PageNumber = 4,
            SectionNumber = "4.1",
            CommentText = "Clarify flow rate unit as mL/min.",
            IsMandatory = true
        }, users.Reviewer.Id);

        Assert.NotNull(findingDto);
        Assert.Equal(ReviewFindingStatus.Open, findingDto.Status);

        // Author responds to finding
        var respondedFinding = await harness.ReviewService.RespondToFindingAsync(
            findingDto.Id,
            new RespondToFindingRequest { Response = "Unit updated to mL/min in table 4." },
            users.Author.Id
        );
        Assert.Equal(ReviewFindingStatus.AuthorResponded, respondedFinding.Status);

        // Reviewer verifies & resolves finding
        await harness.ReviewService.VerifyFindingAsync(
            findingDto.Id,
            new VerifyFindingRequest { VerificationNotes = "Verified and satisfactory." },
            users.Reviewer.Id
        );

        await harness.ReviewService.ResolveFindingAsync(findingDto.Id, users.Reviewer.Id);

        // Complete Review
        var completedReviewDto = await harness.ReviewService.DecideReviewAsync(
            reviewTaskDto.Id,
            new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview,
                ReviewNotes = "All findings resolved."
            },
            users.Reviewer.Id
        );

        Assert.Equal(ReviewTaskStatus.Completed, completedReviewDto.Status);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, completedReviewDto.RevisionStatus);

        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 5: Approval Task Creation with Future Effective Date (WP3)
        // -----------------------------------------------------------------------------------------
        DateTime targetEffective = DateTime.UtcNow.AddDays(14); // Future effective date
        var approvalTask = await harness.ApprovalService.CreateApprovalTaskAsync(
            rev2Dto.Id,
            new CreateApprovalTaskRequest(
                ApproverUserId: users.Approver.Id,
                TargetEffectiveDate: targetEffective,
                SubmissionNotes: "Submitted for QA approval with 14-day future effective date."
            ),
            users.Author.Id
        );

        Assert.NotNull(approvalTask);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, approvalTask.Status);

        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 6: 21 CFR Part 11 Electronic Signature Ceremony (WP4)
        // -----------------------------------------------------------------------------------------
        // Test invalid password rejection first (assert security barrier)
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            harness.ApprovalService.ExecuteApprovalDecisionAsync(
                approvalTask.Id,
                new ExecuteApprovalDecisionRequest(
                    Decision: DocumentApprovalDecision.Approve,
                    DecisionNotes: "Testing rejection",
                    Password: "WrongPassword123!"
                ),
                users.Approver.Id,
                "127.0.0.1"
            )
        );

        // Execute legitimate Part 11 Signature Ceremony
        var approvedTask = await harness.ApprovalService.ExecuteApprovalDecisionAsync(
            approvalTask.Id,
            new ExecuteApprovalDecisionRequest(
                Decision: DocumentApprovalDecision.Approve,
                DecisionNotes: "Approved following complete technical review and verification.",
                Password: DefaultPassword
            ),
            users.Approver.Id,
            "192.168.1.100"
        );

        Assert.Equal(DocumentApprovalTaskStatus.Approved, approvedTask.Status);

        // Verify relational ElectronicSignature in PostgreSQL
        var sigRecord = await db.ElectronicSignatures
            .FirstOrDefaultAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == rev2Dto.Id);

        Assert.NotNull(sigRecord);
        Assert.Equal(users.Approver.Id, sigRecord.UserId);
        Assert.Equal(SignatureMeaning.Approved, sigRecord.MeaningOfSignature);

        // Verify Revision transitioned to FutureEffective
        rev2InDb = await db.DocumentRevisions.FindAsync(rev2Dto.Id);
        Assert.Equal(DocumentRevisionStatus.FutureEffective, rev2InDb!.RevisionStatus);
        Assert.Equal(targetEffective.Date, rev2InDb.EffectiveDate!.Value.Date);

        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 7: Effective Date Automation Worker Execution (WP5)
        // -----------------------------------------------------------------------------------------
        // Worker runs when date is not yet reached -> 0 activated
        var dryRunResult = await harness.WorkerService.ProcessMaturedRevisionsAsync(DateTime.UtcNow.AddDays(5));
        Assert.Equal(0, dryRunResult.SuccessfullyActivatedCount);

        // Advance simulated time past targetEffective
        var workerRunResult = await harness.WorkerService.ProcessMaturedRevisionsAsync(targetEffective.AddMinutes(5));
        Assert.True(workerRunResult.SuccessfullyActivatedCount >= 1);

        // Verify Revision 02 is now Effective
        rev2InDb = await db.DocumentRevisions.FindAsync(rev2Dto.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, rev2InDb!.RevisionStatus);

        // Verify DocumentMaster.CurrentEffectiveRevisionId updated to Revision 02
        var updatedMaster = await db.DocumentMasters.FindAsync(master.Id);
        Assert.Equal(rev2Dto.Id, updatedMaster!.CurrentEffectiveRevisionId);

        // Verify Revision 01 is now Superseded
        var rev1InDb = await db.DocumentRevisions.FindAsync(initialRevision.Id);
        Assert.Equal(DocumentRevisionStatus.Superseded, rev1InDb!.RevisionStatus);

        // Verify Worker Audit Log attributed to ActorType.System
        var workerAudit = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == rev2Dto.Id && a.ActionCode == "RevisionAutomaticallyActivated")
            .FirstOrDefaultAsync();
        Assert.NotNull(workerAudit);
        Assert.Equal(ActorType.System, workerAudit.ActorType);

        // -----------------------------------------------------------------------------------------
        // WP8 Lifecycle Point 8: Periodic Review Engine Generation & Execution (WP6)
        // -----------------------------------------------------------------------------------------
        // Artificially advance next review date to make it due for periodic review
        rev2InDb.NextReviewDate = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var prGenResult = await harness.PeriodicReviewService.GenerateDueReviewTasksAsync();
        Assert.True(prGenResult.CreatedTasksCount >= 1);

        var prTask = await db.PeriodicReviewTasks
            .FirstOrDefaultAsync(t => t.DocumentRevisionId == rev2Dto.Id && t.Status == PeriodicReviewTaskStatus.Pending);
        Assert.NotNull(prTask);
        Assert.Equal(prTask.ReviewCycleMonths, rev2InDb.ReviewCycleMonths);

        // Execute Periodic Review Decision: RemainsValid
        var completedPrTask = await harness.PeriodicReviewService.CompleteReviewAsync(
            prTask.Id,
            new CompletePeriodicReviewRequest(
                Outcome: PeriodicReviewOutcome.RemainsValid,
                ReviewSummary: "Annual review completed. All methods remain current and suitable."
            ),
            users.Reviewer.Id
        );

        Assert.Equal(PeriodicReviewTaskStatus.Completed, completedPrTask.Status);
        Assert.Equal(PeriodicReviewOutcome.RemainsValid, completedPrTask.Outcome);

        // Verify NextReviewDate was advanced by 12 months
        rev2InDb = await db.DocumentRevisions.FindAsync(rev2Dto.Id);
        var expectedNextReview = DateTime.UtcNow.AddMonths(12).Date;
        Assert.Equal(expectedNextReview.Year, rev2InDb!.NextReviewDate!.Value.Year);
        Assert.Equal(expectedNextReview.Month, rev2InDb.NextReviewDate.Value.Month);
    }

    [PostgresFact]
    public async Task WP8_PeriodicReview_RevisionRequired_Handoff_Postgres()
    {
        await using var db = _fixture.CreateDbContext();
        var users = await SeedUsersAsync(db);
        var harness = CreateHarness(db);

        // Seed an effective revision due for periodic review
        string docCode = $"SOP-PR-REV-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
        var master = await harness.MasterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: docCode,
            Title: "Periodic Review Needs Revision SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: users.Author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Lab",
            Keywords: new List<string> { "PR", "RevisionRequired" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 12
        ), users.Author.Id);

        var rev1 = await db.DocumentRevisions.FirstAsync(r => r.DocumentMasterId == master.Id);
        rev1.RevisionStatus = DocumentRevisionStatus.Effective;
        rev1.EffectiveDate = DateTime.UtcNow.AddYears(-1);
        rev1.ReviewCycleMonths = 12;
        rev1.NextReviewDate = DateTime.UtcNow.AddDays(-3);
        var masterEntity = await db.DocumentMasters.FindAsync(master.Id);
        masterEntity!.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        // Generate PR Task
        await harness.PeriodicReviewService.GenerateDueReviewTasksAsync();
        var prTask = await db.PeriodicReviewTasks.FirstAsync(t => t.DocumentRevisionId == rev1.Id);

        // Record a finding during periodic review
        await harness.PeriodicReviewService.AddFindingAsync(prTask.Id, new CreatePeriodicReviewFindingRequest(
            PageNumber: 3,
            SectionNumber: "3.2",
            NoteText: "Reagents listed are no longer commercially supplied."
        ), users.Reviewer.Id);

        // Complete Periodic Review with Recommendation = RevisionRequired
        await harness.PeriodicReviewService.CompleteReviewAsync(prTask.Id, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.RevisionRequired,
            ReviewSummary: "Updating reagent sourcing requires formal revision."
        ), users.Reviewer.Id);

        var updatedPrTask = await db.PeriodicReviewTasks.FindAsync(prTask.Id);
        Assert.Equal(PeriodicReviewOutcome.RevisionRequired, updatedPrTask!.Outcome);

        // Create Revision 02 referencing this Originating Periodic Review Task
        var rev2Dto = await harness.RevisionService.CreateRevisionFromEffectiveAsync(master.Id, new CreateRevisionRequest(
            RevisionType: RevisionType.Minor,
            ReasonForRevision: "PR task finding resolution",
            ChangeSummary: "Updated reagents per periodic review findings",
            ChangeReference: $"PR-TASK-{prTask.Id}",
            OriginatingPeriodicReviewTaskId: prTask.Id
        ), users.Author.Id);

        Assert.Equal("01.1", rev2Dto.RevisionNumber);

        // Convert periodic review finding to RevisionChangeItem
        var prFinding = await db.PeriodicReviewFindings.FirstAsync(f => f.PeriodicReviewTaskId == prTask.Id);
        await harness.RevisionService.AddChangeItemAsync(rev2Dto.Id, new AddChangeItemRequest(
            SectionNumber: prFinding.SectionNumber ?? "3.2",
            SectionTitle: "Reagent Specifications",
            DescriptionOfChange: prFinding.NoteText,
            ChangeRationale: "Periodic Review finding resolution",
            ChangeCategory: "Modification",
            Status: "Draft"
        ), users.Author.Id);

        // Verify that the change item is recorded
        var changeItems = await db.RevisionChangeItems.Where(c => c.DocumentRevisionId == rev2Dto.Id).ToListAsync();
        Assert.NotEmpty(changeItems);
        Assert.Contains(changeItems, c => c.SectionNumber == "3.2");
    }

    [PostgresFact]
    public async Task WP8_PeriodicReview_ObsolescenceRecommended_Approval_Postgres()
    {
        await using var db = _fixture.CreateDbContext();
        var users = await SeedUsersAsync(db);
        var harness = CreateHarness(db);

        // Seed an effective document
        string docCode = $"SOP-OBS-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
        var master = await harness.MasterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: docCode,
            Title: "Obsolescence Candidate SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: users.Author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Lab",
            Keywords: new List<string> { "Obsolescence", "PR" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 12
        ), users.Author.Id);

        var rev1 = await db.DocumentRevisions.FirstAsync(r => r.DocumentMasterId == master.Id);
        rev1.RevisionStatus = DocumentRevisionStatus.Effective;
        rev1.NextReviewDate = DateTime.UtcNow.AddDays(-2);
        var masterEntity = await db.DocumentMasters.FindAsync(master.Id);
        masterEntity!.CurrentEffectiveRevisionId = rev1.Id;
        await db.SaveChangesAsync();

        // Generate PR Task
        await harness.PeriodicReviewService.GenerateDueReviewTasksAsync();
        var prTask = await db.PeriodicReviewTasks.FirstAsync(t => t.DocumentRevisionId == rev1.Id);

        // Complete Periodic Review with ObsolescenceRecommended
        await harness.PeriodicReviewService.CompleteReviewAsync(prTask.Id, new CompletePeriodicReviewRequest(
            Outcome: PeriodicReviewOutcome.ObsolescenceRecommended,
            ReviewSummary: "Testing process decommissioned. Recommend full obsolescence."
        ), users.Reviewer.Id);

        // Verify that an obsolescence DocumentApprovalTask was automatically spawned
        var approvalTask = await db.DocumentApprovalTasks
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == rev1.Id);

        Assert.NotNull(approvalTask);
        Assert.Equal(DocumentApprovalTaskStatus.Pending, approvalTask.Status);
    }

    [PostgresFact]
    public async Task WP8_SegregationOfDuties_CrossRoleEnforcement_Postgres()
    {
        await using var db = _fixture.CreateDbContext();
        var users = await SeedUsersAsync(db);
        var harness = CreateHarness(db);

        string docCode = $"SOP-SOD-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
        var master = await harness.MasterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: docCode,
            Title: "Segregation of Duties Enforcement SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: users.Author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Compliance",
            Keywords: new List<string> { "SoD", "Part11" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 12
        ), users.Author.Id);

        var rev1 = await db.DocumentRevisions.FirstAsync(r => r.DocumentMasterId == master.Id);

        // 1. Author cannot be designated as Reviewer
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.ReviewService.SubmitForReviewAsync(rev1.Id, new SubmitForReviewRequest
            {
                ReviewerUserId = users.Author.Id, // Violation!
                SubmissionNotes = "Author reviewing own doc"
            }, users.Author.Id)
        );

        // 2. Author cannot submit approval task nominating themselves as Approver
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.ApprovalService.CreateApprovalTaskAsync(rev1.Id, new CreateApprovalTaskRequest(
                ApproverUserId: users.Author.Id, // Violation!
                TargetEffectiveDate: DateTime.UtcNow.AddDays(7),
                SubmissionNotes: "Author self-approval"
            ), users.Author.Id)
        );

        // 3. System Administrator cannot bypass SoD to approve author's document if admin was author
        string adminDocCode = $"SOP-ADM-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
        var adminMaster = await harness.MasterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: adminDocCode,
            Title: "Admin Authored SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: users.Admin.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Compliance",
            Keywords: new List<string> { "Admin", "SoD" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 12
        ), users.Admin.Id);

        var adminRev = await db.DocumentRevisions.FirstAsync(r => r.DocumentMasterId == adminMaster.Id);

        // Admin attempting to create approval task nominating themselves as Approver
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.ApprovalService.CreateApprovalTaskAsync(adminRev.Id, new CreateApprovalTaskRequest(
                ApproverUserId: users.Admin.Id,
                TargetEffectiveDate: DateTime.UtcNow.AddDays(7),
                SubmissionNotes: "Admin approving own document"
            ), users.Admin.Id)
        );
    }

    [PostgresFact]
    public async Task WP8_Concurrency_DoubleWorkerActivation_Postgres()
    {
        await using var db = _fixture.CreateDbContext();
        var users = await SeedUsersAsync(db);
        var harness = CreateHarness(db);

        string docCode = $"SOP-RACE-{Guid.NewGuid():N}".Substring(0, 16).ToUpper();
        var master = await harness.MasterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: docCode,
            Title: "Concurrency Worker SOP",
            DocumentTypeId: _fixture.SeededDocTypeId,
            DepartmentId: _fixture.SeededDepartmentId,
            SectionId: _fixture.SeededSectionId,
            DocumentOwnerUserId: users.Author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "IT",
            Keywords: new List<string> { "Concurrency" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 12
        ), users.Author.Id);

        var rev1 = await db.DocumentRevisions.FirstAsync(r => r.DocumentMasterId == master.Id);
        rev1.RevisionStatus = DocumentRevisionStatus.FutureEffective;
        rev1.EffectiveDate = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        // 1. First execution activates the revision
        var result1 = await harness.WorkerService.ProcessMaturedRevisionsAsync(DateTime.UtcNow);
        Assert.Equal(1, result1.SuccessfullyActivatedCount);

        // 2. Second execution immediately follows (idempotent, 0 activated)
        var result2 = await harness.WorkerService.ProcessMaturedRevisionsAsync(DateTime.UtcNow);
        Assert.Equal(0, result2.SuccessfullyActivatedCount);

        // Verify PostgreSQL state
        await using var verifyDb = _fixture.CreateDbContext();
        var finalRev = await verifyDb.DocumentRevisions.FindAsync(rev1.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, finalRev!.RevisionStatus);

        // Exactly one activation audit entry exists in PostgreSQL
        var audits = await verifyDb.AuditLogs
            .Where(l => l.ActionCode == "RevisionAutomaticallyActivated" && l.DocumentRevisionId == rev1.Id)
            .ToListAsync();
        Assert.Single(audits);
    }
}
