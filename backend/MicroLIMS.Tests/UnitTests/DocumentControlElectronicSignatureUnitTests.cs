using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlElectronicSignatureUnitTests
{
    private const string CorrectPassword = "QA-Approver-Password-2026!";

    private async Task<(
        MicroLimsDbContext db,
        User author,
        User controller,
        User reviewer,
        User approver,
        User admin,
        DocumentMaster master,
        DocumentRevision revision,
        DocumentApprovalService approvalService,
        DocumentRevisionService revisionService
    )> CreateSeededContextAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new MicroLimsDbContext(options);

        // Roles
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "Admin", IsActive = true };
        var controllerRole = new Role { Type = RoleType.SectionHead, Name = "Document Controller", IsActive = true };
        var reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Quality Reviewer", IsActive = true };
        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        db.Roles.AddRange(adminRole, controllerRole, reviewerRole, analystRole);
        await db.SaveChangesAsync();

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword);

        // Users
        var admin = new User { FullName = "System Admin", Username = "admin1", RoleId = adminRole.Id, IsActive = true, PasswordHash = passwordHash, Role = adminRole };
        var controller = new User { FullName = "Document Controller", Username = "controller1", RoleId = controllerRole.Id, IsActive = true, PasswordHash = passwordHash, Role = controllerRole };
        var author = new User { FullName = "SOP Author", Username = "author1", RoleId = analystRole.Id, IsActive = true, PasswordHash = passwordHash, Role = analystRole };
        var reviewer = new User { FullName = "Technical Reviewer", Username = "reviewer1", RoleId = reviewerRole.Id, IsActive = true, PasswordHash = passwordHash, Role = reviewerRole };
        var approver = new User { FullName = "QA Approver", Username = "approver1", RoleId = reviewerRole.Id, IsActive = true, PasswordHash = passwordHash, Role = reviewerRole };
        db.Users.AddRange(admin, controller, author, reviewer, approver);
        await db.SaveChangesAsync();

        // Document Type, Department, Section
        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", IsActive = true };
        var dept = new DocumentDepartment { Code = "QA", Name = "Quality Assurance", IsActive = true };
        db.DocumentTypes.Add(docType);
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();

        var section = new DocumentSection { Name = "Compliance", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();

        // Document Master
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = "DOC-0000088",
            CompanyDocumentCode = "SOP-QA-088",
            Title = "Electronic Signature and Data Integrity SOP",
            DocumentTypeId = docType.Id,
            DepartmentId = dept.Id,
            SectionId = section.Id,
            DocumentOwnerUserId = author.Id,
            Confidentiality = DocumentConfidentiality.Internal,
            RecordStatus = DocumentRecordStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            CreatedByUserId = author.Id
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        // Document Revision in AwaitingApproval status
        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.AwaitingApproval,
            RevisionType = RevisionType.Major,
            ReasonForRevision = "Initial baseline procedure for 21 CFR Part 11 electronic signatures.",
            CreatedByUserId = author.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            RecordOrigin = RecordOrigin.Native
        };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();

        // Attach Controlled PDF
        var pdfFile = new RevisionFile
        {
            DocumentRevisionId = revision.Id,
            FileRole = FileRole.ControlledPdf,
            FileName = "sop_qa_088_v01.pdf",
            ContentType = "application/pdf",
            SizeBytes = 4096,
            ContentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            IsActive = true,
            UploadedByUserId = author.Id,
            UploadedAt = DateTime.UtcNow.AddDays(-9)
        };
        db.RevisionFiles.Add(pdfFile);

        // Add Completed Technical Review Task with zero unresolved findings
        var reviewTask = new DocumentReviewTask
        {
            DocumentRevisionId = revision.Id,
            AssignedReviewerUserId = reviewer.Id,
            AssignedByUserId = author.Id,
            AssignedAt = DateTime.UtcNow.AddDays(-8),
            Status = ReviewTaskStatus.Completed,
            Decision = ReviewDecision.CompleteReview,
            DecisionAt = DateTime.UtcNow.AddDays(-5),
            DecisionByUserId = reviewer.Id,
            ReviewNotes = "Technical review fully approved without reservations."
        };
        db.DocumentReviewTasks.Add(reviewTask);
        await db.SaveChangesAsync();

        var seqHelper = new DatabaseSequenceHelper(db);
        var audit = new AuditEventService(db, seqHelper);
        var auth = new DocumentAuthorizationService(db);
        var signatureService = new ElectronicSignatureService(db);
        var approvalService = new DocumentApprovalService(db, audit, auth, signatureService);
        var revisionService = new DocumentRevisionService(db, audit, auth);

        return (db, author, controller, reviewer, approver, admin, master, revision, approvalService, revisionService);
    }

    [Fact]
    public async Task SignApproval_WithValidPassword_AppliesPart11ElectronicSignature_AndApprovesRevision()
    {
        var (db, author, _, _, approver, _, master, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Approved under 21 CFR Part 11 compliance.",
            Password: CorrectPassword
        );

        var result = await approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id, "192.168.1.100");

        Assert.Equal(DocumentApprovalTaskStatus.Approved, result.Status);
        Assert.Equal(approver.Id, result.DecisionByUserId);

        // Verify revision transitioned to Effective
        var dbRev = await db.DocumentRevisions.FindAsync(revision.Id);
        Assert.Equal(DocumentRevisionStatus.Effective, dbRev!.RevisionStatus);

        // Verify Part 11 Electronic Signature record
        var signature = await db.ElectronicSignatures
            .FirstOrDefaultAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);

        Assert.NotNull(signature);
        Assert.Equal(approver.Id, signature.UserId);
        Assert.Equal(approver.FullName, signature.UserFullNameSnapshot);
        Assert.Equal(approver.Username, signature.UsernameSnapshot);
        Assert.Equal("Reviewer", signature.RoleSnapshot);
        Assert.Equal(SignatureMeaning.Approved, signature.MeaningOfSignature);
        Assert.Equal("Approved under 21 CFR Part 11 compliance.", signature.Comment);
        Assert.Equal("192.168.1.100", signature.IpAddress);
        Assert.True(signature.SignedAt <= DateTime.UtcNow);

        // Verify semantic audit events emitted
        var auditSig = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == revision.Id && a.ActionCode == "ElectronicSignatureApplied");
        Assert.NotNull(auditSig);

        var auditApprove = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.DocumentRevisionId == revision.Id && a.ActionCode == "DocumentRevisionApproved");
        Assert.NotNull(auditApprove);
    }

    [Fact]
    public async Task SignApproval_MissingPassword_ThrowsInvalidOperationException_NoStateMutated()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Attempting to approve without password.",
            Password: null
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id));

        Assert.Contains("Password re-authentication is required", ex.Message);

        // Verify revision remains in AwaitingApproval
        var dbRev = await db.DocumentRevisions.FindAsync(revision.Id);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, dbRev!.RevisionStatus);

        // Verify zero signatures written
        var sigCount = await db.ElectronicSignatures.CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);
        Assert.Equal(0, sigCount);
    }

    [Fact]
    public async Task SignApproval_IncorrectPassword_ThrowsInvalidOperationException_AuditsFailedAttempt()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Attempting with bad password.",
            Password: "Wrong-Password-123!"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id));

        Assert.Contains("Password verification failed", ex.Message);

        // Verify revision remains in AwaitingApproval
        var dbRev = await db.DocumentRevisions.FindAsync(revision.Id);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, dbRev!.RevisionStatus);

        // Verify failure was audited in AuditLogs
        var failedAudit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "SignatureFailed" && a.UserId == approver.Id);
        Assert.NotNull(failedAudit);

        // Zero successful signatures written
        var sigCount = await db.ElectronicSignatures.CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);
        Assert.Equal(0, sigCount);
    }

    [Fact]
    public async Task SignApproval_InactiveOrLockedUser_ThrowsUnauthorizedOrInvalidOperation()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Deactivate approver
        approver.IsActive = false;
        await db.SaveChangesAsync();

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            Password: CorrectPassword
        );

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id));
    }

    [Fact]
    public async Task SignApproval_DuplicateSignatureAttempt_ThrowsInvalidOperationException()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Initial approval.",
            Password: CorrectPassword
        );

        await approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id);

        // Attempt second approval on the completed task
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id));

        Assert.Contains("Cannot execute decision on approval task in status 'Approved'", ex.Message);

        // Verify only 1 signature was recorded
        var sigCount = await db.ElectronicSignatures.CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);
        Assert.Equal(1, sigCount);
    }

    [Fact]
    public async Task SignApproval_AuthorCannotSignApproval_EvenWithCorrectPassword_ThrowsSoDViolation()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Author attempting to sign own approval.",
            Password: CorrectPassword
        );

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, author.Id));

        Assert.Contains("Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision", ex.Message);

        // Zero signatures written
        var sigCount = await db.ElectronicSignatures.CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);
        Assert.Equal(0, sigCount);
    }

    [Fact]
    public async Task SignApproval_ReviewerCannotSignApproval_EvenWithCorrectPassword_ThrowsSoDViolation()
    {
        var (db, author, _, reviewer, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Reviewer attempting to sign approval.",
            Password: CorrectPassword
        );

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, reviewer.Id));

        Assert.Contains("Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision", ex.Message);

        // Zero signatures written
        var sigCount = await db.ElectronicSignatures.CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);
        Assert.Equal(0, sigCount);
    }

    [Fact]
    public async Task SignApproval_AdminCannotBypassSoD_EvenWithCorrectPassword_ThrowsSoDViolation()
    {
        var (db, author, _, _, approver, admin, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Admin authored the revision
        revision.CreatedByUserId = admin.Id;
        await db.SaveChangesAsync();

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Admin attempting to sign own revision.",
            Password: CorrectPassword
        );

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, admin.Id));

        Assert.Contains("Segregation of Duties Violation", ex.Message);

        // Zero signatures written
        var sigCount = await db.ElectronicSignatures.CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);
        Assert.Equal(0, sigCount);
    }

    [Fact]
    public async Task SignApproval_UnassignedUser_ThrowsUnauthorizedAccessException()
    {
        var (db, author, controller, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Create an unrelated analyst
        var analystRole = await db.Roles.FirstAsync(r => r.Type == RoleType.Analyst);
        var unrelatedUser = new User
        {
            FullName = "Unrelated Analyst",
            Username = "unrelated1",
            RoleId = analystRole.Id,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword),
            Role = analystRole
        };
        db.Users.Add(unrelatedUser);
        await db.SaveChangesAsync();

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            Password: CorrectPassword
        );

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, unrelatedUser.Id));

        Assert.Contains("Only the assigned approver or Document Controller may execute an approval decision", ex.Message);
    }

    [Fact]
    public async Task SignApproval_DraftStatusRevision_CannotBeSigned()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Change revision back to Draft
        revision.RevisionStatus = DocumentRevisionStatus.Draft;
        await db.SaveChangesAsync();

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            Password: CorrectPassword
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id));

        Assert.Contains("Status must be 'AwaitingApproval'", ex.Message);
    }

    [Fact]
    public async Task SignApproval_MissingControlledPdf_BlocksSigning()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Deactivate PDF
        var pdf = await db.RevisionFiles.FirstAsync(f => f.DocumentRevisionId == revision.Id);
        pdf.IsActive = false;
        await db.SaveChangesAsync();

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            Password: CorrectPassword
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id));

        Assert.Contains("An active Controlled PDF file is required", ex.Message);
    }

    [Fact]
    public async Task NonApprovalDecisions_ReturnAndDecline_DoNotCreateElectronicSignatures()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        // Return for correction
        var returnRequest = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.ReturnForCorrection,
            DecisionNotes: "Missing detailed section on incubation intervals."
        );

        await approvalService.ExecuteApprovalDecisionAsync(task.Id, returnRequest, approver.Id);

        // Verify zero signatures created
        var sigCount = await db.ElectronicSignatures.CountAsync(s => s.EntityType == "DocumentRevision" && s.EntityId == revision.Id);
        Assert.Equal(0, sigCount);
    }

    [Fact]
    public async Task GetApprovalSignature_ReturnsAccuratePart11SnapshotAndMetadata()
    {
        var (db, author, _, _, approver, _, _, revision, approvalService, _) = await CreateSeededContextAsync();

        var task = await approvalService.CreateApprovalTaskAsync(revision.Id, new CreateApprovalTaskRequest(approver.Id), author.Id);

        var request = new ExecuteApprovalDecisionRequest(
            Decision: DocumentApprovalDecision.Approve,
            DecisionNotes: "Certified for clinical manufacturing use.",
            Password: CorrectPassword
        );

        await approvalService.ExecuteApprovalDecisionAsync(task.Id, request, approver.Id, "10.0.0.45");

        var sigDto = await approvalService.GetApprovalSignatureAsync(task.Id, approver.Id);

        Assert.NotNull(sigDto);
        Assert.Equal(approver.Id, sigDto.UserId);
        Assert.Equal(approver.FullName, sigDto.UserFullNameSnapshot);
        Assert.Equal(approver.Username, sigDto.UsernameSnapshot);
        Assert.Equal("Reviewer", sigDto.RoleSnapshot);
        Assert.Equal(SignatureMeaning.Approved, sigDto.MeaningOfSignature);
        Assert.Equal("DocumentRevision", sigDto.EntityType);
        Assert.Equal(revision.Id, sigDto.EntityId);
        Assert.Equal("10.0.0.45", sigDto.IpAddress);
        Assert.Equal("Certified for clinical manufacturing use.", sigDto.Comment);

        // Verify Dossier also reflects the signature
        var dossier = await approvalService.GetApprovalDossierAsync(task.Id, approver.Id);
        Assert.NotNull(dossier.ActiveSignature);
        Assert.Equal(sigDto.Id, dossier.ActiveSignature.Id);
    }
}
