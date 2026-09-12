using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlRevisionUnitTests
{
    private readonly DbContextOptions<MicroLimsDbContext> _options;

    public DocumentControlRevisionUnitTests()
    {
        _options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private async Task<(MicroLimsDbContext Db, User Author, User Controller, User Reviewer, DocumentMaster Master, DocumentRevision EffectiveRev)>
        CreateSeededContextWithEffectiveDocumentAsync()
    {
        var db = new MicroLimsDbContext(_options);

        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        var controllerRole = new Role { Type = RoleType.SectionHead, Name = "Document Controller", IsActive = true };
        db.Roles.AddRange(analystRole, controllerRole);
        await db.SaveChangesAsync();

        var author = new User { FullName = "SOP Author", Username = "author1", RoleId = analystRole.Id, IsActive = true, PasswordHash = "x" };
        var controller = new User { FullName = "Document Controller", Username = "controller1", RoleId = controllerRole.Id, IsActive = true, PasswordHash = "x" };
        var reviewer = new User { FullName = "Technical Reviewer", Username = "reviewer1", RoleId = analystRole.Id, IsActive = true, PasswordHash = "x" };
        db.Users.AddRange(author, controller, reviewer);
        await db.SaveChangesAsync();

        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", IsActive = true };
        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var section = new DocumentSection { Name = "Microbiology", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000042",
            CompanyDocumentCode = "SOP-QC-042",
            Title = "Environmental Monitoring SOP",
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

        var effectiveRev = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Effective,
            EffectiveDate = DateTime.UtcNow.AddMonths(-6),
            NextReviewDate = DateTime.UtcNow.AddMonths(18),
            ReviewCycleMonths = 24,
            RevisionType = RevisionType.Major,
            ReasonForRevision = "Initial baseline release for operational go-live.",
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(effectiveRev);
        await db.SaveChangesAsync();

        master.CurrentEffectiveRevisionId = effectiveRev.Id;
        await db.SaveChangesAsync();

        // Attach controlled PDF file to effective revision
        var file = new RevisionFile
        {
            DocumentRevisionId = effectiveRev.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "sop_qc_042_v01.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            ContentSha256 = "abc123sha256",
            IsActive = true,
            UploadedByUserId = author.Id,
            UploadedAt = DateTime.UtcNow.AddMonths(-6)
        };
        db.RevisionFiles.Add(file);
        await db.SaveChangesAsync();

        return (db, author, controller, reviewer, master, effectiveRev);
    }

    [Fact]
    public async Task CreateRevision_FromEffective_Succeeds_RetainingMasterAndMicroLimsId()
    {
        var (db, author, _, _, master, effectiveRev) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var request = new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Annual update of microbial limit criteria and incubation times.",
            ChangeSummary: "Added section 4.2 detailing water testing temperature tolerances.",
            ChangeReference: "CC-2026-0089"
        );

        var result = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, request, author.Id);

        Assert.NotNull(result);
        Assert.Equal(master.Id, result.DocumentMasterId);
        Assert.Equal("02", result.RevisionNumber);
        Assert.Equal(2, result.RevisionSequence);
        Assert.Equal(DocumentRevisionStatus.Draft, result.RevisionStatus);
        Assert.Equal(RevisionType.Major, result.RevisionType);
        Assert.Equal("Annual update of microbial limit criteria and incubation times.", result.ReasonForRevision);
        Assert.Equal("CC-2026-0089", result.ChangeReference);

        // Verify Master permanent identity was strictly preserved
        var masterInDb = await db.DocumentMasters.Include(d => d.Revisions).FirstOrDefaultAsync(d => d.Id == master.Id);
        Assert.Equal("DOC-0000042", masterInDb!.MicroLimsDocumentId);
        Assert.Equal("SOP-QC-042", masterInDb.CompanyDocumentCode);
        Assert.Equal(effectiveRev.Id, masterInDb.CurrentEffectiveRevisionId); // Still effective!
        Assert.Equal(2, masterInDb.Revisions.Count);

        // Prior effective revision remains Effective
        var priorRev = await db.DocumentRevisions.FindAsync(effectiveRev.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, priorRev!.RevisionStatus);
    }

    [Fact]
    public async Task ProposeNextRevision_MajorAndMinor_IncrementsCorrectly()
    {
        var (db, author, _, _, master, effectiveRev) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        // Major proposes 02
        var majorRes = await revisionService.ProposeNextRevisionAsync(master.Id, RevisionType.Major, author.Id);
        Assert.Equal("02", majorRes.ProposedRevisionNumber);
        Assert.Equal("01", majorRes.CurrentEffectiveRevisionNumber);

        // Minor proposes 01.1
        var minorRes = await revisionService.ProposeNextRevisionAsync(master.Id, RevisionType.Minor, author.Id);
        Assert.Equal("01.1", minorRes.ProposedRevisionNumber);
    }

    [Fact]
    public async Task CreateRevision_AuthorizedOverride_SucceedsAndAudits()
    {
        var (db, _, controller, _, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var request = new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Harmonization with global parent SOP structure.",
            CustomRevisionNumber: "02.A"
        );

        var result = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, request, controller.Id);

        Assert.Equal("02.A", result.RevisionNumber);

        // Verify override audit trail
        var auditLogs = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == result.Id && a.ActionCode == "RevisionNumberOverridden")
            .ToListAsync();
        Assert.Single(auditLogs);
        Assert.Contains("manually overridden", auditLogs.First().Reason);
    }

    [Fact]
    public async Task CreateRevision_UnauthorizedOverride_ThrowsUnauthorized()
    {
        var (db, author, _, _, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var request = new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Harmonization with global parent SOP structure.",
            CustomRevisionNumber: "02-SPECIAL"
        );

        // Author does not have Document Controller role
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            revisionService.CreateRevisionFromEffectiveAsync(master.Id, request, author.Id));

        Assert.Contains("Only Document Controllers are authorized to override", ex.Message);
    }

    // DC-URS-166 / BR-013 negative test. The revision-number override is
    // reserved for the Document Controller, and the FRS-1A permission matrix
    // records System Administrator as No for it. The service used to admit
    // SystemAdministrator here - contradicting its own error message and the
    // rule that administrators get no exemption from controlled-action
    // restrictions.
    [Fact]
    public async Task CreateRevision_OverrideBySystemAdministrator_IsRefused()
    {
        var (db, _, _, _, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();

        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "System Administrator", IsActive = true };
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();

        var admin = new User { FullName = "System Administrator", Username = "admin1", RoleId = adminRole.Id, IsActive = true, PasswordHash = "x" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var request = new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Administrative renumbering requested outside change control.",
            CustomRevisionNumber: "99-ADMIN"
        );

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            revisionService.CreateRevisionFromEffectiveAsync(master.Id, request, admin.Id));

        Assert.Contains("Only Document Controllers are authorized to override", ex.Message);
    }

    [Fact]
    public async Task CreateRevision_MandatoryReasonValidation_RejectsShortOrEmpty()
    {
        var (db, author, _, _, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        // Empty reason
        var reqEmpty = new CreateRevisionRequest(RevisionType.Major, "");
        var exEmpty = await Assert.ThrowsAsync<ArgumentException>(() =>
            revisionService.CreateRevisionFromEffectiveAsync(master.Id, reqEmpty, author.Id));
        Assert.Contains("Reason for Revision is mandatory", exEmpty.Message);

        // Short reason (< 10 chars)
        var reqShort = new CreateRevisionRequest(RevisionType.Major, "Short");
        var exShort = await Assert.ThrowsAsync<ArgumentException>(() =>
            revisionService.CreateRevisionFromEffectiveAsync(master.Id, reqShort, author.Id));
        Assert.Contains("at least 10 characters", exShort.Message);
    }

    [Fact]
    public async Task CreateRevision_Blocked_WhenDraftAlreadyInFlight()
    {
        var (db, author, _, _, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var validReq = new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "First active revision draft in flight."
        );

        // Create first draft
        await revisionService.CreateRevisionFromEffectiveAsync(master.Id, validReq, author.Id);

        // Attempt second draft while first is in draft
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            revisionService.CreateRevisionFromEffectiveAsync(master.Id, validReq, author.Id));

        Assert.Contains("Cannot create a new revision while another revision is currently in draft", ex.Message);
    }

    [Fact]
    public async Task StructuredChangeItems_AddUpdateDelete_PersistsAndAudits()
    {
        var (db, author, _, _, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var draft = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Structured change items lifecycle test."
        ), author.Id);

        // 1. Add Change Item
        var addReq = new AddChangeItemRequest(
            SectionNumber: "4.2",
            SectionTitle: "Sample Preparation",
            DescriptionOfChange: "Increased homogenizer speed to 2000 RPM.",
            ChangeRationale: "Satisfies compendial update USP <61>.",
            ChangeCategory: "Modification"
        );

        var item = await revisionService.AddChangeItemAsync(draft.Id, addReq, author.Id);
        Assert.NotNull(item);
        Assert.Equal("4.2", item.SectionNumber);
        Assert.Equal("Draft", item.Status);

        // 2. Update Change Item
        var updateReq = new UpdateChangeItemRequest(
            SectionNumber: "4.2",
            SectionTitle: "Sample Preparation & Dilution",
            DescriptionOfChange: "Increased homogenizer speed to 2000 RPM for 60 seconds.",
            ChangeRationale: "Satisfies compendial update USP <61>.",
            ChangeCategory: "Modification",
            Status: "Addressed"
        );

        var updated = await revisionService.UpdateChangeItemAsync(item.Id, updateReq, author.Id);
        Assert.Equal("Addressed", updated.Status);
        Assert.Equal("Sample Preparation & Dilution", updated.SectionTitle);

        // 3. Query list
        var list = await revisionService.GetChangeItemsAsync(draft.Id, author.Id);
        Assert.Single(list);

        // 4. Remove Change Item. DC-URS-184 and BR-015 forbid permanent
        // deletion of a workflow record, so this deactivates and retains it:
        // it leaves the active list but must still exist in the database.
        await revisionService.DeactivateChangeItemAsync(item.Id, author.Id);
        var listAfter = await revisionService.GetChangeItemsAsync(draft.Id, author.Id);
        Assert.Empty(listAfter);

        var retained = await db.RevisionChangeItems.FirstOrDefaultAsync(c => c.Id == item.Id);
        Assert.NotNull(retained);
        Assert.False(retained!.IsActive);
        Assert.Equal("Increased homogenizer speed to 2000 RPM for 60 seconds.", retained.DescriptionOfChange);

        // Removing it twice is rejected rather than silently re-applied.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => revisionService.DeactivateChangeItemAsync(item.Id, author.Id));

        // 5. Verify audit logs
        var auditLogs = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == draft.Id)
            .OrderBy(a => a.Id)
            .ToListAsync();
        Assert.Contains(auditLogs, a => a.ActionCode == "RevisionChangeItemAdded");
        Assert.Contains(auditLogs, a => a.ActionCode == "RevisionChangeItemUpdated");
        Assert.Contains(auditLogs, a => a.ActionCode == "RevisionChangeItemDeactivated");
    }

    [Fact]
    public async Task StructuredChangeItems_ConvertReviewFinding_PreservesLinkage()
    {
        var (db, author, _, reviewer, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var draft = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Draft for findings conversion."
        ), author.Id);

        // Seed a review finding
        var task = new DocumentReviewTask
        {
            DocumentRevisionId = draft.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = author.Id,
            Status = ReviewTaskStatus.InProgress
        };
        db.DocumentReviewTasks.Add(task);
        await db.SaveChangesAsync();

        var finding = new DocumentReviewFinding
        {
            DocumentReviewTaskId = task.Id,
            PageNumber = 3,
            SectionNumber = "2.1",
            CommentText = "Missing references to secondary incubation temperatures.",
            IsMandatory = true,
            Status = ReviewFindingStatus.Open,
            CreatedByUserId = reviewer.Id
        };
        db.DocumentReviewFindings.Add(finding);
        await db.SaveChangesAsync();

        // Convert finding into structured change item
        var convertReq = new ConvertFindingRequest(
            SectionTitle: "Incubation Conditions",
            ChangeRationale: "Addressing reviewer finding from prior review round.",
            ChangeCategory: "Clarification"
        );

        var changeItem = await revisionService.ConvertFindingToChangeItemAsync(draft.Id, finding.Id, convertReq, author.Id);

        Assert.NotNull(changeItem);
        Assert.Equal(finding.Id, changeItem.OriginatingReviewFindingId);
        Assert.Equal("2.1", changeItem.SectionNumber);
        Assert.Equal(finding.CommentText, changeItem.DescriptionOfChange);

        var auditLogs = await db.AuditLogs
            .Where(a => a.DocumentRevisionId == draft.Id && a.ActionCode == "ReviewFindingConvertedToChangeItem")
            .ToListAsync();
        Assert.Single(auditLogs);
    }

    [Fact]
    public async Task RevisionImpactAssessment_MultiCategoryValidation_EnforcesDetails()
    {
        var (db, author, _, _, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        var draft = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Draft for impact assessment."
        ), author.Id);

        // Invalid: TrainingImpact is true but details is empty (< 5 chars)
        var invalidReq = new SaveImpactAssessmentRequest(
            ProcedureOrMethodImpact: false, ProcedureOrMethodDetails: null,
            TrainingImpact: true, TrainingDetails: "No", // < 5 chars
            FormsOrTemplatesImpact: false, FormsOrTemplatesDetails: null,
            SpecificationsImpact: false, SpecificationsDetails: null,
            EquipmentImpact: false, EquipmentDetails: null,
            MaterialsOrMediaImpact: false, MaterialsOrMediaDetails: null,
            ValidationImpact: false, ValidationDetails: null,
            RegulatoryCommitmentImpact: false, RegulatoryCommitmentDetails: null,
            RelatedDocumentsImpact: false, RelatedDocumentsDetails: null
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            revisionService.SaveImpactAssessmentAsync(draft.Id, invalidReq, author.Id));
        Assert.Contains("Details are required for 'Training' impact", ex.Message);

        // Valid assessment
        var validReq = new SaveImpactAssessmentRequest(
            ProcedureOrMethodImpact: true, ProcedureOrMethodDetails: "Incubation time changed from 48h to 72h.",
            TrainingImpact: true, TrainingDetails: "Requires retraining of all microbiology analysts.",
            FormsOrTemplatesImpact: false, FormsOrTemplatesDetails: null,
            SpecificationsImpact: false, SpecificationsDetails: null,
            EquipmentImpact: false, EquipmentDetails: null,
            MaterialsOrMediaImpact: false, MaterialsOrMediaDetails: null,
            ValidationImpact: false, ValidationDetails: null,
            RegulatoryCommitmentImpact: false, RegulatoryCommitmentDetails: null,
            RelatedDocumentsImpact: false, RelatedDocumentsDetails: null
        );

        var saved = await revisionService.SaveImpactAssessmentAsync(draft.Id, validReq, author.Id);
        Assert.NotNull(saved);
        Assert.True(saved.IsComplete);
        Assert.True(saved.ProcedureOrMethodImpact);
        Assert.True(saved.TrainingImpact);

        // Verify retrieval
        var retrieved = await revisionService.GetImpactAssessmentAsync(draft.Id, author.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Incubation time changed from 48h to 72h.", retrieved!.ProcedureOrMethodDetails);
    }

    [Fact]
    public async Task SubmissionGuard_BlocksReviewSubmission_IfImpactAssessmentIncomplete()
    {
        var (db, author, _, reviewer, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        // Create incremented revision (RevisionSequence = 2)
        var draft = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Draft without impact assessment completion."
        ), author.Id);

        // Attach active controlled PDF
        var file = new RevisionFile
        {
            DocumentRevisionId = draft.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "sop_v02.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            ContentSha256 = "dummy",
            IsActive = true,
            UploadedByUserId = author.Id
        };
        db.RevisionFiles.Add(file);
        await db.SaveChangesAsync();

        // Attempt review submission without impact assessment
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.SubmitForReviewAsync(draft.Id, new SubmitForReviewRequest
            {
                ReviewerUserId = reviewer.Id
            }, author.Id));

        Assert.Contains("Revision Impact Assessment must be completed", ex.Message);
    }

    [Fact]
    public async Task SubmissionGuard_BlocksReviewSubmission_IfOriginatingFindingsUnaddressed()
    {
        var (db, author, _, reviewer, master, _) = await CreateSeededContextWithEffectiveDocumentAsync();
        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var revisionService = new DocumentRevisionService(db, audit, auth);
        var reviewService = new DocumentReviewService(db, audit, auth);

        var draft = await revisionService.CreateRevisionFromEffectiveAsync(master.Id, new CreateRevisionRequest(
            RevisionType: RevisionType.Major,
            ReasonForRevision: "Draft with unaddressed originating findings."
        ), author.Id);

        // Attach controlled PDF
        var file = new RevisionFile
        {
            DocumentRevisionId = draft.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "sop_v02.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            ContentSha256 = "dummy",
            IsActive = true,
            UploadedByUserId = author.Id
        };
        db.RevisionFiles.Add(file);
        await db.SaveChangesAsync();

        // Complete impact assessment
        await revisionService.SaveImpactAssessmentAsync(draft.Id, new SaveImpactAssessmentRequest(
            ProcedureOrMethodImpact: false, ProcedureOrMethodDetails: null,
            TrainingImpact: false, TrainingDetails: null,
            FormsOrTemplatesImpact: false, FormsOrTemplatesDetails: null,
            SpecificationsImpact: false, SpecificationsDetails: null,
            EquipmentImpact: false, EquipmentDetails: null,
            MaterialsOrMediaImpact: false, MaterialsOrMediaDetails: null,
            ValidationImpact: false, ValidationDetails: null,
            RegulatoryCommitmentImpact: false, RegulatoryCommitmentDetails: null,
            RelatedDocumentsImpact: false, RelatedDocumentsDetails: null
        ), author.Id);

        // Add a change item with OriginatingReviewFindingId in status "Draft" (not "Addressed")
        var changeItem = new RevisionChangeItem
        {
            DocumentRevisionId = draft.Id,
            SectionNumber = "3.1",
            SectionTitle = "Sampling",
            DescriptionOfChange = "Originating finding item",
            ChangeRationale = "Mandatory item",
            ChangeCategory = "Modification",
            Status = "Draft", // Unaddressed!
            OriginatingReviewFindingId = 999,
            CreatedByUserId = author.Id
        };
        db.RevisionChangeItems.Add(changeItem);
        await db.SaveChangesAsync();

        // Attempt review submission
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reviewService.SubmitForReviewAsync(draft.Id, new SubmitForReviewRequest
            {
                ReviewerUserId = reviewer.Id
            }, author.Id));

        Assert.Contains("originating review finding(s) remain unaddressed", ex.Message);

        // Mark addressed -> now submission succeeds!
        changeItem.Status = "Addressed";
        await db.SaveChangesAsync();

        var submittedTask = await reviewService.SubmitForReviewAsync(draft.Id, new SubmitForReviewRequest
        {
            ReviewerUserId = reviewer.Id
        }, author.Id);

        Assert.NotNull(submittedTask);
        Assert.Equal(ReviewTaskStatus.Pending, submittedTask.Status);
    }
}
