using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Email;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Owner decision Q13: the five Document Control codes are a role gate on top
// of the assignment/ownership rules. Each action needs BOTH.
public class DocumentControlPermissionEnforcementTests
{
    private sealed class StubRequestContext : ISecurityRequestContext
    {
        public string? CorrelationId => "test";
        public string? IpAddress => "203.0.113.7";
        public string? UserAgent => "MicroLIMS-Test/1.0";
        public string? RequestPath => "/api/x";
        public string Source => "Api";
    }

    private readonly MicroLimsDbContext _db;
    private readonly IDocumentAuthorizationService _auth;
    private readonly DocumentMasterService _masterService;
    private readonly DocumentFileService _fileService;
    private readonly DocumentReviewService _reviewService;
    private readonly DocumentRevisionService _revisionService;
    private readonly DocumentApprovalService _approvalService;
    private readonly PeriodicReviewService _periodicService;

    private readonly Role _bareRole;       // custom role holding no Document Control code
    private readonly Role _reviewerRole;   // seeded Reviewer role (Review, PeriodicReview, Approve)
    private readonly Role _analystRole;    // seeded Analyst role (DraftEdit, RevisionCreate)
    private readonly Role _controllerRole;
    private readonly User _author;         // Analyst: owns and authors the document
    private readonly User _reviewer;       // Reviewer role
    private readonly User _approver;       // Reviewer role
    private readonly User _bare;           // bare role
    private readonly User _controller;
    private readonly int _docTypeId, _deptId, _sectionId;

    public DocumentControlPermissionEnforcementTests()
    {
        _db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var seq = new DatabaseSequenceHelper(_db);
        IAuditEventService audit = new AuditEventService(_db, seq);
        _auth = new DocumentAuthorizationService(_db);
        _masterService = new DocumentMasterService(_db, seq, audit, _auth);
        _fileService = new DocumentFileService(_db, new InMemoryFileStorageService(), audit, _auth);
        _reviewService = new DocumentReviewService(_db, audit, _auth);
        _revisionService = new DocumentRevisionService(_db, audit, _auth);
        _approvalService = new DocumentApprovalService(_db, audit, _auth, new AcceptingSignatureService(_db));
        _periodicService = new PeriodicReviewService(_db, audit, _auth, _approvalService, NullLogger<PeriodicReviewService>.Instance);

        var admin = new Role { Type = RoleType.SystemAdministrator, Name = "Admin", IsSystemRole = true, IsActive = true };
        _controllerRole = new Role { Type = RoleType.SectionHead, Name = "Document Controller", IsSystemRole = true, IsActive = true };
        _reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsSystemRole = true, IsActive = true };
        _analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true, IsActive = true };
        _bareRole = new Role { Type = RoleType.Reviewer, Name = "Bare reviewer", IsSystemRole = false, IsActive = true };
        // (added after the seed below: the seeder maps one role per type)
        _db.Roles.AddRange(admin, _controllerRole, _reviewerRole, _analystRole);
        _db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(_db);
        _db.Roles.Add(_bareRole);
        _db.SaveChanges();

        User U(string n, Role r) => new() { Username = n, FullName = n, Email = $"{n}@lims.local", PasswordHash = "x", RoleId = r.Id, IsActive = true };
        _author = U("author", _analystRole);
        _reviewer = U("reviewer", _reviewerRole);
        _approver = U("approver", _reviewerRole);
        _bare = U("bare", _bareRole);
        _controller = U("controller", _controllerRole);
        _db.Users.AddRange(_author, _reviewer, _approver, _bare, _controller);
        _db.SaveChanges();

        var type = new DocumentType { Code = "SOP", Name = "SOP", DefaultReviewCycleMonths = 24, IsActive = true };
        var dept = new DocumentDepartment { Code = "QC", Name = "QC", IsActive = true };
        _db.DocumentTypes.Add(type);
        _db.DocumentDepartments.Add(dept);
        _db.SaveChanges();
        var sec = new DocumentSection { DepartmentId = dept.Id, Name = "Micro", IsActive = true };
        _db.DocumentSections.Add(sec);
        _db.SaveChanges();
        _docTypeId = type.Id; _deptId = dept.Id; _sectionId = sec.Id;
    }

    private async Task GrantAsync(Role role, string code)
    {
        var perm = await _db.Permissions.SingleAsync(p => p.Code == code);
        if (!await _db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id))
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            await _db.SaveChangesAsync();
        }
    }

    private async Task RevokeAsync(Role role, string code)
    {
        var rows = await _db.RolePermissions.Where(rp => rp.RoleId == role.Id && rp.Permission!.Code == code).ToListAsync();
        _db.RolePermissions.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    private async Task<(int MasterId, int RevisionId)> SeedDraftAsync(int? ownerId = null)
    {
        var doc = await _masterService.RegisterDocumentMasterAsync(new RegisterDocumentMasterRequest(
            CompanyDocumentCode: $"SOP-{Guid.NewGuid():N}".Substring(0, 14),
            Title: "Permission enforcement SOP",
            DocumentTypeId: _docTypeId, DepartmentId: _deptId, SectionId: _sectionId,
            DocumentOwnerUserId: ownerId ?? _author.Id,
            Confidentiality: DocumentConfidentiality.Internal,
            Category: "Testing", Keywords: new List<string> { "QC" },
            InitialRevisionNumber: "01", ReviewCycleMonths: 24), _author.Id);
        await _fileService.UploadRevisionFileAsync(doc.Revisions[0].Id, FileRole.ControlledPdf, "a.pdf", "application/pdf",
            Encoding.UTF8.GetBytes("%PDF-1.4 mock"), _controller.Id);
        return (doc.Id, doc.Revisions[0].Id);
    }

    // ---- Documents.DraftEdit ------------------------------------------------------------

    [Fact]
    public async Task DraftEdit_OwnerWhoseRoleLacksTheCode_IsRefused_AllowedOnceGranted()
    {
        var (masterId, revId) = await SeedDraftAsync(ownerId: _bare.Id);

        Assert.False(await _auth.CanEditDraftMetadataAsync(masterId, _bare.Id));
        Assert.False(await _auth.CanUploadOrReplaceDraftFileAsync(revId, _bare.Id));
        Assert.False(await _auth.CanCancelDraftRevisionAsync(revId, _bare.Id));

        await GrantAsync(_bareRole, PermissionConstants.DocumentsDraftEdit);

        Assert.True(await _auth.CanEditDraftMetadataAsync(masterId, _bare.Id));
        Assert.True(await _auth.CanUploadOrReplaceDraftFileAsync(revId, _bare.Id));
        Assert.True(await _auth.CanCancelDraftRevisionAsync(revId, _bare.Id));
    }

    [Fact]
    public async Task DraftEdit_HoldingTheCodeAloneIsNotEnough_NeedsOwnershipOrAssignment()
    {
        var (masterId, _) = await SeedDraftAsync();
        await GrantAsync(_bareRole, PermissionConstants.DocumentsDraftEdit);

        Assert.False(await _auth.CanEditDraftMetadataAsync(masterId, _bare.Id));
    }

    [Fact]
    public async Task DraftEdit_DocumentControllerRoleShortcutDoesNotBypassTheCode()
    {
        var (masterId, revId) = await SeedDraftAsync();
        Assert.True(await _auth.CanEditDraftMetadataAsync(masterId, _controller.Id));

        await RevokeAsync(_controllerRole, PermissionConstants.DocumentsDraftEdit);

        Assert.False(await _auth.CanEditDraftMetadataAsync(masterId, _controller.Id));
        Assert.False(await _auth.CanCancelDraftRevisionAsync(revId, _controller.Id));
    }

    [Fact]
    public async Task DraftEdit_UpdateMetadataServiceRefusesWithoutTheCode()
    {
        var (masterId, _) = await SeedDraftAsync();
        await RevokeAsync(_analystRole, PermissionConstants.DocumentsDraftEdit);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _masterService.UpdateDraftMetadataAsync(masterId,
            new UpdateDocumentMasterDraftRequest("SOP-X", "New title", _docTypeId, _deptId, _sectionId, _author.Id, DocumentConfidentiality.Internal, "c", null, null), _author.Id));
    }

    // ---- Documents.RevisionCreate -------------------------------------------------------

    [Fact]
    public async Task RevisionCreate_RefusedWithoutTheCode_EvenForTheOwnerWithDraftEdit()
    {
        var (masterId, _) = await SeedDraftAsync();
        await RevokeAsync(_analystRole, PermissionConstants.DocumentsRevisionCreate);

        Assert.True(await _auth.CanEditDraftMetadataAsync(masterId, _author.Id));
        Assert.False(await _auth.CanCreateRevisionAsync(masterId, _author.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _revisionService.CreateRevisionFromEffectiveAsync(masterId, new CreateRevisionRequest(RevisionType.Minor, "annual update"), _author.Id));

        await GrantAsync(_analystRole, PermissionConstants.DocumentsRevisionCreate);
        Assert.True(await _auth.CanCreateRevisionAsync(masterId, _author.Id));
    }

    // ---- Documents.Review ---------------------------------------------------------------

    [Fact]
    public async Task Review_AssigningAReviewerWhoseRoleLacksTheCode_IsRejected()
    {
        var (_, revId) = await SeedDraftAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _bare.Id }, _author.Id));
        Assert.Contains("Documents.Review", ex.Message);

        var ok = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewer.Id }, _author.Id);
        Assert.Equal(ReviewTaskStatus.Pending, ok.Status);
    }

    [Fact]
    public async Task Review_AssignedReviewerWhoseRoleLostTheCode_CannotDecide_AllowedWithBoth()
    {
        var (_, revId) = await SeedDraftAsync();
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewer.Id }, _author.Id);

        await RevokeAsync(_reviewerRole, PermissionConstants.DocumentsReview);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest { Decision = ReviewDecision.CompleteReview, ReviewNotes = "ok" }, _reviewer.Id));
        Assert.Contains("Documents.Review", ex.Message);

        await GrantAsync(_reviewerRole, PermissionConstants.DocumentsReview);
        var done = await _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest { Decision = ReviewDecision.CompleteReview, ReviewNotes = "ok" }, _reviewer.Id);
        Assert.Equal(ReviewTaskStatus.Completed, done.Status);
    }

    [Fact]
    public async Task Review_HoldingTheCodeButNotAssigned_StillRefused()
    {
        var (_, revId) = await SeedDraftAsync();
        var task = await _reviewService.SubmitForReviewAsync(revId, new SubmitForReviewRequest { ReviewerUserId = _reviewer.Id }, _author.Id);

        // _approver's role holds Documents.Review but is not the assigned reviewer.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _reviewService.DecideReviewAsync(task.Id, new ReviewDecisionRequest { Decision = ReviewDecision.CompleteReview, ReviewNotes = "ok" }, _approver.Id));
    }

    // ---- Documents.Approve --------------------------------------------------------------

    private async Task<int> SeedPendingApprovalTaskAsync(int approverId)
    {
        var (_, revId) = await SeedDraftAsync();
        var task = new DocumentApprovalTask
        {
            DocumentRevisionId = revId,
            AssignedApproverUserId = approverId,
            AssignedByUserId = _author.Id,
            Status = DocumentApprovalTaskStatus.Pending
        };
        _db.DocumentApprovalTasks.Add(task);
        await _db.SaveChangesAsync();
        return task.Id;
    }

    [Fact]
    public async Task Approve_DesignatedApproverWhoseRoleLacksTheCode_IsRefused()
    {
        var taskId = await SeedPendingApprovalTaskAsync(_approver.Id);
        await RevokeAsync(_reviewerRole, PermissionConstants.DocumentsApprove);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _approvalService.ExecuteApprovalDecisionAsync(taskId, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Decline, "no"), _approver.Id));
        Assert.Contains("Documents.Approve", ex.Message);
    }

    [Fact]
    public async Task Approve_DesignatedApproverWithTheCode_PassesTheRoleGate()
    {
        var taskId = await SeedPendingApprovalTaskAsync(_approver.Id);
        Exception? thrown = null;
        try { await _approvalService.ExecuteApprovalDecisionAsync(taskId, new ExecuteApprovalDecisionRequest(DocumentApprovalDecision.Decline, "no"), _approver.Id); }
        catch (Exception e) { thrown = e; }
        // Whatever later rule applies, it is not the role gate.
        Assert.DoesNotContain("Documents.Approve", thrown?.Message ?? "");
    }

    [Fact]
    public async Task Approve_CreatingATaskForAnApproverWhoseRoleLacksTheCode_IsRejected()
    {
        var (_, revId) = await SeedDraftAsync();
        var rev = await _db.DocumentRevisions.FindAsync(revId);
        rev!.RevisionStatus = DocumentRevisionStatus.AwaitingApproval;
        _db.DocumentReviewTasks.Add(new DocumentReviewTask
        {
            DocumentRevisionId = revId, AssignedReviewerUserId = _reviewer.Id, AssignedByUserId = _author.Id, Status = ReviewTaskStatus.Completed
        });
        await _db.SaveChangesAsync();
        await RevokeAsync(_reviewerRole, PermissionConstants.DocumentsApprove);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _approvalService.CreateApprovalTaskAsync(revId, new CreateApprovalTaskRequest(_approver.Id), _author.Id));
        Assert.Contains("Documents.Approve", ex.Message);
    }

    // ---- Documents.PeriodicReview -------------------------------------------------------

    private async Task<int> SeedPeriodicTaskAsync(int reviewerId)
    {
        var (masterId, revId) = await SeedDraftAsync();
        var task = new PeriodicReviewTask
        {
            DocumentMasterId = masterId, DocumentRevisionId = revId, ReviewCycleMonths = 24,
            ScheduledDueDate = DateTime.UtcNow, AssignedReviewerUserId = reviewerId, Status = PeriodicReviewTaskStatus.Pending
        };
        _db.PeriodicReviewTasks.Add(task);
        await _db.SaveChangesAsync();
        return task.Id;
    }

    [Fact]
    public async Task PeriodicReview_AssignedReviewerWhoseRoleLacksTheCode_IsRefused()
    {
        var taskId = await SeedPeriodicTaskAsync(_reviewer.Id);
        await RevokeAsync(_reviewerRole, PermissionConstants.DocumentsPeriodicReview);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _periodicService.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(PeriodicReviewOutcome.RemainsValid, "Still valid and current."), _reviewer.Id));
        Assert.Contains("Documents.PeriodicReview", ex.Message);
    }

    [Fact]
    public async Task PeriodicReview_AssignedReviewerWithTheCode_Completes()
    {
        var taskId = await SeedPeriodicTaskAsync(_reviewer.Id);
        var done = await _periodicService.CompleteReviewAsync(taskId, new CompletePeriodicReviewRequest(PeriodicReviewOutcome.RemainsValid, "Still valid and current."), _reviewer.Id);
        Assert.Equal(PeriodicReviewTaskStatus.Completed, done.Status);
    }

    [Fact]
    public async Task PeriodicReview_AssigningAReviewerWhoseRoleLacksTheCode_IsRejected()
    {
        var taskId = await SeedPeriodicTaskAsync(_reviewer.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _periodicService.AssignReviewerAsync(taskId, new AssignPeriodicReviewerRequest(_bare.Id), _controller.Id));
        Assert.Contains("Documents.PeriodicReview", ex.Message);
    }

    // ---- Assignment pickers -------------------------------------------------------------

    [Theory]
    [InlineData(AssignmentRole.Author)]
    [InlineData(AssignmentRole.TechnicalReviewer)]
    [InlineData(AssignmentRole.Approver)]
    public async Task Assignment_OfAUserWhoseRoleLacksTheMatchingCode_IsRejected(AssignmentRole role)
    {
        var (masterId, _) = await SeedDraftAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _masterService.AddOrUpdateAssignmentAsync(masterId, new CreateAssignmentRequest(_bare.Id, role), _author.Id));
    }

    [Fact]
    public async Task Assignment_OfUsersWhoseRolesHoldTheCode_Succeeds()
    {
        var (masterId, _) = await SeedDraftAsync();

        await _masterService.AddOrUpdateAssignmentAsync(masterId, new CreateAssignmentRequest(_reviewer.Id, AssignmentRole.TechnicalReviewer), _author.Id);
        await _masterService.AddOrUpdateAssignmentAsync(masterId, new CreateAssignmentRequest(_approver.Id, AssignmentRole.Approver), _author.Id);
        await _masterService.AddOrUpdateAssignmentAsync(masterId, new CreateAssignmentRequest(_controller.Id, AssignmentRole.Author), _author.Id);
    }

    [Fact]
    public async Task Directory_FilteredByPermission_ExcludesUsersWhoseRoleLacksIt()
    {
        var securityAudit = new SecurityAuditService(_db, new StubRequestContext());
        var authService = new AuthenticationService(
            _db, subject => $"jwt-for-{subject.UserId}", new PermissionService(_db),
            new EmailSender("", 587, "", "", "no-reply@microlims.local", false),
            NullLogger<AuthenticationService>.Instance, securityAudit);
        var users = new UserService(_db, authService, securityAudit, signatureService: new AcceptingSignatureService(_db));
        var all = await users.GetDirectoryAsync();
        var reviewers = await users.GetDirectoryAsync(PermissionConstants.DocumentsReview);

        Assert.Contains(all, u => u.Id == _bare.Id);
        Assert.DoesNotContain(reviewers, u => u.Id == _bare.Id);
        Assert.DoesNotContain(reviewers, u => u.Id == _author.Id);
        Assert.Contains(reviewers, u => u.Id == _reviewer.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => users.GetDirectoryAsync(PermissionConstants.UsersManage));
    }
}
