using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlReviewUnitTests
{
    private readonly MicroLimsDbContext _db;
    private readonly IDatabaseSequenceHelper _seqHelper;
    private readonly IAuditEventService _auditService;
    private readonly IDocumentAuthorizationService _authService;
    private readonly InMemoryFileStorageService _storage;
    private readonly DocumentMasterService _masterService;
    private readonly DocumentFileService _fileService;
    private readonly DocumentReviewService _reviewService;

    private readonly int _adminUserId;
    private readonly int _controllerUserId;
    private readonly int _authorUserId;
    private readonly int _reviewerUserId;
    private readonly int _unauthorizedUserId;
    private readonly int _docTypeId;
    private readonly int _deptId;
    private readonly int _sectionId;

    public DocumentControlReviewUnitTests()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new MicroLimsDbContext(options);
        _seqHelper = new DatabaseSequenceHelper(_db);
        _auditService = new AuditEventService(_db, _seqHelper);
        _authService = new DocumentAuthorizationService(_db);
        _storage = new InMemoryFileStorageService();

        _masterService = new DocumentMasterService(_db, _seqHelper, _auditService, _authService);
        _fileService = new DocumentFileService(_db, _storage, _auditService, _authService);
        _reviewService = new DocumentReviewService(_db, _auditService, _authService);

        // Seed Roles
        var adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "Admin", IsActive = true };
        var controllerRole = new Role { Type = RoleType.SectionHead, Name = "Document Controller", IsActive = true };
        var analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsActive = true };
        _db.Roles.AddRange(adminRole, controllerRole, analystRole);
        _db.SaveChanges();

        // Seed Users
        var adminUser = new User { Username = "admin", FullName = "System Admin", Email = "admin@lims.local", PasswordHash = "x", RoleId = adminRole.Id, IsActive = true };
        var controllerUser = new User { Username = "controller", FullName = "Doc Controller", Email = "dc@lims.local", PasswordHash = "x", RoleId = controllerRole.Id, IsActive = true };
        var authorUser = new User { Username = "author", FullName = "QC Analyst Author", Email = "author@lims.local", PasswordHash = "x", RoleId = analystRole.Id, IsActive = true };
        var reviewerUser = new User { Username = "reviewer", FullName = "QC Lead Reviewer", Email = "rev@lims.local", PasswordHash = "x", RoleId = analystRole.Id, IsActive = true };
        var unauthUser = new User { Username = "other", FullName = "External User", Email = "other@lims.local", PasswordHash = "x", RoleId = analystRole.Id, IsActive = true };
        _db.Users.AddRange(adminUser, controllerUser, authorUser, reviewerUser, unauthUser);
        _db.SaveChanges();

        _adminUserId = adminUser.Id;
        _controllerUserId = controllerUser.Id;
        _authorUserId = authorUser.Id;
        _reviewerUserId = reviewerUser.Id;
        _unauthorizedUserId = unauthUser.Id;

        // Seed Type, Department, Section
        var docType = new DocumentType { Code = "SOP", Name = "Standard Operating Procedure", DefaultReviewCycleMonths = 24, IsActive = true };
        var dept = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        _db.DocumentTypes.Add(docType);
        _db.DocumentDepartments.Add(dept);
        _db.SaveChanges();

        var sec = new DocumentSection { DepartmentId = dept.Id, Name = "Microbiology", IsActive = true };
        _db.DocumentSections.Add(sec);
        _db.SaveChanges();

        _docTypeId = docType.Id;
        _deptId = dept.Id;
        _sectionId = sec.Id;
    }

    private async Task<(int MasterId, int RevisionId)> SeedDraftDocumentAsync(bool attachPdf = true)
    {
        var req = new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-MIC-{Guid.NewGuid():N}".Substring(0, 16),
            Title: "Sterility Testing Standard Operating Procedure",
            DocumentTypeId: _docTypeId,
            DepartmentId: _deptId,
            SectionId: _sectionId,
            DocumentOwnerUserId: _authorUserId,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Testing Procedures",
            Keywords: new List<string> { "QC", "Testing", "Microbiology" },
            InitialRevisionNumber: "01",
            ReviewCycleMonths: 24
        );

        var doc = await _masterService.RegisterDocumentMasterAsync(req, _authorUserId);
        var revId = doc.Revisions[0].Id;

        if (attachPdf)
        {
            var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock Controlled PDF Content");
            await _fileService.UploadRevisionFileAsync(
                revId,
                FileRole.ControlledPdf,
                "sop_sterility_v1.pdf",
                "application/pdf",
                pdfBytes,
                _authorUserId);
        }

        return (doc.Id, revId);
    }

    [Fact]
    public async Task SubmitForReview_Success_TransitionsToInReview_CreatesPendingTask()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);

        var req = new SubmitForReviewRequest
        {
            ReviewerUserId = _reviewerUserId,
            SubmissionNotes = "Ready for technical review.",
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        var task = await _reviewService.SubmitForReviewAsync(revId, req, _authorUserId);

        Assert.NotNull(task);
        Assert.Equal(ReviewTaskStatus.Pending, task.Status);
        Assert.Equal(DocumentRevisionStatus.InReview, task.RevisionStatus);
        Assert.Equal(_reviewerUserId, task.AssignedReviewerUserId);

        var revInDb = await _db.DocumentRevisions.FindAsync(revId);
        Assert.Equal(DocumentRevisionStatus.InReview, revInDb!.RevisionStatus);
    }

    [Fact]
    public async Task SubmitForReview_Fails_IfNoActivePdf()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: false);

        var req = new SubmitForReviewRequest
        {
            ReviewerUserId = _reviewerUserId,
            SubmissionNotes = "Draft without PDF"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.SubmitForReviewAsync(revId, req, _authorUserId));
    }

    [Fact]
    public async Task SoD_AuthorCannotAssignSelfAsReviewer()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);

        var req = new SubmitForReviewRequest
        {
            ReviewerUserId = _authorUserId, // SELF ASSIGNMENT
            SubmissionNotes = "Attempting self-review"
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _reviewService.SubmitForReviewAsync(revId, req, _authorUserId));

        Assert.Contains("Segregation of Duties", ex.Message);
    }

    [Fact]
    public async Task SoD_AuthorCannotBeAssignedAsReviewer_EvenByAdmin()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);

        var req = new SubmitForReviewRequest
        {
            ReviewerUserId = _authorUserId, // Admin attempts to assign the author as reviewer
            SubmissionNotes = "Admin override attempt"
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _reviewService.SubmitForReviewAsync(revId, req, _adminUserId));

        Assert.Contains("Segregation of Duties", ex.Message);
    }

    [Fact]
    public async Task AddReviewFinding_Success_AdvancesTaskToInProgress()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        var findingReq = new AddReviewFindingRequest
        {
            PageNumber = 3,
            SectionNumber = "5.1",
            CommentText = "Please clarify incubation temperature tolerance bounds.",
            IsMandatory = true
        };

        var finding = await _reviewService.AddReviewFindingAsync(task.Id, findingReq, _reviewerUserId);

        Assert.NotNull(finding);
        Assert.Equal(ReviewFindingStatus.Open, finding.Status);
        Assert.Equal("5.1", finding.SectionNumber);
        Assert.True(finding.IsMandatory);

        var taskInDb = await _db.DocumentReviewTasks.FindAsync(task.Id);
        Assert.Equal(ReviewTaskStatus.InProgress, taskInDb!.Status);
    }

    [Fact]
    public async Task SoD_AuthorCannotCreateReviewerFinding()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        var findingReq = new AddReviewFindingRequest
        {
            CommentText = "Author trying to add finding",
            IsMandatory = true
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _reviewService.AddReviewFindingAsync(task.Id, findingReq, _authorUserId));

        Assert.Contains("Segregation of Duties", ex.Message);
    }

    [Fact]
    public async Task CompleteReviewCommentLifecycle_Open_Responded_Verified_Resolved()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        // 1. Reviewer posts finding (Open)
        var finding = await _reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            PageNumber = 2,
            CommentText = "Add reference to USP <71> section 4.",
            IsMandatory = true
        }, _reviewerUserId);
        Assert.Equal(ReviewFindingStatus.Open, finding.Status);

        // 2. Author responds (AuthorResponded)
        var responded = await _reviewService.RespondToFindingAsync(finding.Id, new RespondToFindingRequest
        {
            Response = "Section 4 reference added on page 2."
        }, _authorUserId);
        Assert.Equal(ReviewFindingStatus.AuthorResponded, responded.Status);
        Assert.Equal("Section 4 reference added on page 2.", responded.AuthorResponse);

        // 3. Reviewer verifies (ReviewerVerified)
        var verified = await _reviewService.VerifyFindingAsync(finding.Id, new VerifyFindingRequest
        {
            VerificationNotes = "Verified reference matches latest compendial edition."
        }, _reviewerUserId);
        Assert.Equal(ReviewFindingStatus.ReviewerVerified, verified.Status);

        // 4. Reviewer resolves (Resolved)
        var resolved = await _reviewService.ResolveFindingAsync(finding.Id, _reviewerUserId);
        Assert.Equal(ReviewFindingStatus.Resolved, resolved.Status);
    }

    [Fact]
    public async Task SoD_ReviewerConcurrenceGate_AuthorCannotCloseOrResolveReviewerFinding()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        var finding = await _reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            CommentText = "Critical safety note missing.",
            IsMandatory = true
        }, _reviewerUserId);

        // Author attempts to resolve/close reviewer finding
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _reviewService.ResolveFindingAsync(finding.Id, _authorUserId));

        Assert.Contains("Segregation of Duties", ex.Message);
    }

    [Fact]
    public async Task MandatoryFindingsGate_CompleteReview_BlockedIfMandatoryFindingOpen()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        // Reviewer creates mandatory finding
        await _reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            CommentText = "Mandatory correction required.",
            IsMandatory = true
        }, _reviewerUserId);

        // Reviewer attempts to complete review while finding is Open
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview,
                ReviewNotes = "Attempting completion"
            }, _reviewerUserId));

        Assert.Contains("mandatory review finding(s) remain unresolved", ex.Message);
    }

    [Fact]
    public async Task DecideReview_CompleteReview_SucceedsWhenAllMandatoryFindingsResolved()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        var finding = await _reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            CommentText = "Mandatory correction required.",
            IsMandatory = true
        }, _reviewerUserId);

        // Resolve finding
        await _reviewService.ResolveFindingAsync(finding.Id, _reviewerUserId);

        // Now complete review
        var completedTask = await _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.CompleteReview,
            ReviewNotes = "Technical review passed without objections."
        }, _reviewerUserId);

        Assert.Equal(ReviewTaskStatus.Completed, completedTask.Status);
        Assert.Equal(ReviewDecision.CompleteReview, completedTask.Decision);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, completedTask.RevisionStatus);

        var revInDb = await _db.DocumentRevisions.FindAsync(revId);
        Assert.Equal(DocumentRevisionStatus.AwaitingApproval, revInDb!.RevisionStatus);
    }

    [Fact]
    public async Task DecideReview_ReturnForCorrection_RevertsRevisionToDraft()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        var returnedTask = await _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.ReturnForCorrection,
            ReviewNotes = "Major updates required to equipment section."
        }, _reviewerUserId);

        Assert.Equal(ReviewTaskStatus.ReturnedForCorrection, returnedTask.Status);
        Assert.Equal(ReviewDecision.ReturnForCorrection, returnedTask.Decision);
        Assert.Equal(DocumentRevisionStatus.Draft, returnedTask.RevisionStatus);

        var revInDb = await _db.DocumentRevisions.FindAsync(revId);
        Assert.Equal(DocumentRevisionStatus.Draft, revInDb!.RevisionStatus);
    }

    [Fact]
    public async Task SoD_AuthorCannotCompleteReviewOfOwnRevision()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
            {
                Decision = ReviewDecision.CompleteReview
            }, _authorUserId));

        Assert.Contains("Segregation of Duties", ex.Message);
    }

    [Fact]
    public async Task AuditTrail_RecordsFullReviewLifecycle()
    {
        var (_, revId) = await SeedDraftDocumentAsync(attachPdf: true);
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewerUserId }, _authorUserId);

        var finding = await _reviewService.AddReviewFindingAsync(task.Id, new AddReviewFindingRequest
        {
            CommentText = "Sample finding.",
            IsMandatory = false
        }, _reviewerUserId);

        await _reviewService.ResolveFindingAsync(finding.Id, _reviewerUserId);

        await _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest
        {
            Decision = ReviewDecision.CompleteReview
        }, _reviewerUserId);

        var auditLogs = await _db.AuditLogs
            .Where(a => a.DocumentRevisionId == revId)
            .Select(a => a.ActionCode)
            .ToListAsync();

        Assert.Contains("RevisionSubmittedForReview", auditLogs);
        Assert.Contains("ReviewFindingCreated", auditLogs);
        Assert.Contains("ReviewFindingResolved", auditLogs);
        Assert.Contains("TechnicalReviewCompleted", auditLogs);
    }
}
