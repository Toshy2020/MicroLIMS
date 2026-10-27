using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Owner decision: documents are owned by their department with a named custodian. Deactivating a user moves
// nothing; their open responsibilities form a per-department "needs reassignment" queue computed from the data,
// and an authorised person hands each one to an active, eligible member of that department (reason, audit,
// notification). Nothing is ever reassigned automatically.
public class DocumentReassignmentTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly MicroLimsDbContext _db;
    private readonly Clock _clock = new();
    private readonly SpyNotificationService _push = new();
    private readonly DocumentReassignmentService _svc;
    private readonly DocumentAuthorizationService _auth;
    private readonly AuditEventService _audit;

    private Role _adminRole = null!, _headRole = null!, _reviewerRole = null!, _analystRole = null!;
    private int _deptA, _deptB, _sectionA, _sectionB, _docType;
    private User _admin = null!, _headA = null!, _headB = null!, _authorA = null!, _candA = null!, _revA = null!, _rev2A = null!, _outsider = null!, _gone = null!, _goneRev = null!;
    private DocumentMaster _m1 = null!, _m2 = null!, _mVoid = null!, _mActive = null!;
    private DocumentRevision _r1 = null!;
    private DocumentMasterAssignment _authorAssign = null!, _reviewerAssign = null!, _approverAssign = null!, _ownerAssign = null!;
    private DocumentReviewTask _reviewTask = null!;
    private DocumentApprovalTask _approvalTask = null!;
    private PeriodicReviewTask _periodic = null!;

    public DocumentReassignmentTests()
    {
        _db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _audit = new AuditEventService(_db, new DatabaseSequenceHelper(_db));
        _auth = new DocumentAuthorizationService(_db);
        _svc = NewService();
        Seed();
    }

    private DocumentReassignmentService NewService(ISystemSettingsReader? settings = null) =>
        new(_db, _audit, _auth, _push, settings, _clock);

    private User U(string name, Role role, bool active = true, DateTime? deactivatedAt = null) =>
        new() { Username = name, FullName = name, Email = $"{name}@lims.local", PasswordHash = "x", RoleId = role.Id, IsActive = active, DeactivatedAt = deactivatedAt };

    private void Member(User user, int departmentId, int? sectionId = null) =>
        _db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = departmentId, SectionId = sectionId });

    private DocumentMaster Master(string code, int departmentId, int sectionId, User owner, DocumentRecordStatus status = DocumentRecordStatus.Active) =>
        new()
        {
            MicroLimsDocumentId = "DOC-" + code, CompanyDocumentCode = code, Title = "Title " + code, DocumentTypeId = _docType,
            DepartmentId = departmentId, SectionId = sectionId, DocumentOwnerUserId = owner.Id, CreatedByUserId = _authorA.Id,
            Confidentiality = DocumentConfidentiality.Internal, RecordStatus = status
        };

    private void Seed()
    {
        _adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "Admin", IsSystemRole = true, IsActive = true };
        _headRole = new Role { Type = RoleType.SectionHead, Name = "Section Head", IsSystemRole = true, IsActive = true };
        _reviewerRole = new Role { Type = RoleType.Reviewer, Name = "Reviewer", IsSystemRole = true, IsActive = true };
        _analystRole = new Role { Type = RoleType.Analyst, Name = "Analyst", IsSystemRole = true, IsActive = true };
        _db.Roles.AddRange(_adminRole, _headRole, _reviewerRole, _analystRole);
        _db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(_db);

        var type = new DocumentType { Code = "SOP", Name = "SOP", DefaultReviewCycleMonths = 24, IsActive = true };
        var a = new DocumentDepartment { Code = "QC", Name = "Quality Control", IsActive = true };
        var b = new DocumentDepartment { Code = "PD", Name = "Production", IsActive = true };
        _db.DocumentTypes.Add(type);
        _db.DocumentDepartments.AddRange(a, b);
        _db.SaveChanges();
        var secA = new DocumentSection { DepartmentId = a.Id, Name = "Micro", Code = "MICRO", IsActive = true };
        var secB = new DocumentSection { DepartmentId = b.Id, Name = "Packing", Code = "PACK", IsActive = true };
        _db.DocumentSections.AddRange(secA, secB);
        _db.SaveChanges();
        (_docType, _deptA, _deptB, _sectionA, _sectionB) = (type.Id, a.Id, b.Id, secA.Id, secB.Id);

        var longAgo = _clock.Now.UtcDateTime.AddDays(-10);
        var recently = _clock.Now.UtcDateTime.AddDays(-2);
        _admin = U("admin", _adminRole);
        _headA = U("headA", _headRole);
        _headB = U("headB", _headRole);
        _authorA = U("authorA", _analystRole);
        _candA = U("candA", _analystRole);
        _revA = U("revA", _reviewerRole);
        _rev2A = U("rev2A", _reviewerRole);
        _outsider = U("outsider", _reviewerRole);
        _gone = U("gone", _analystRole, active: false, deactivatedAt: longAgo);
        _goneRev = U("goneRev", _reviewerRole, active: false, deactivatedAt: recently);
        _db.Users.AddRange(_admin, _headA, _headB, _authorA, _candA, _revA, _rev2A, _outsider, _gone, _goneRev);
        _db.SaveChanges();

        Member(_headA, _deptA); Member(_headB, _deptB);
        Member(_authorA, _deptA); Member(_candA, _deptA, _sectionA); // a section membership counts as membership of the department
        Member(_revA, _deptA); Member(_rev2A, _deptA);
        Member(_outsider, _deptB);
        _db.SaveChanges();

        _m1 = Master("SOP-001", _deptA, _sectionA, _gone);
        _m2 = Master("SOP-002", _deptB, _sectionB, _gone);
        _mVoid = Master("SOP-003", _deptA, _sectionA, _gone, DocumentRecordStatus.Void);
        _mActive = Master("SOP-004", _deptA, _sectionA, _authorA);
        _db.DocumentMasters.AddRange(_m1, _m2, _mVoid, _mActive);
        _db.SaveChanges();

        _r1 = new DocumentRevision { DocumentMasterId = _m1.Id, RevisionNumber = "02", RevisionSequence = 2, RevisionStatus = DocumentRevisionStatus.InReview, CreatedByUserId = _authorA.Id };
        _db.DocumentRevisions.AddRange(
            _r1,
            new DocumentRevision { DocumentMasterId = _m2.Id, RevisionNumber = "01", RevisionSequence = 1, RevisionStatus = DocumentRevisionStatus.Draft, CreatedByUserId = _authorA.Id });
        _db.SaveChanges();

        _authorAssign = new DocumentMasterAssignment { DocumentMasterId = _m1.Id, UserId = _gone.Id, AssignmentRole = AssignmentRole.Author, AssignedByUserId = _admin.Id };
        _reviewerAssign = new DocumentMasterAssignment { DocumentMasterId = _m1.Id, UserId = _goneRev.Id, AssignmentRole = AssignmentRole.TechnicalReviewer, AssignedByUserId = _admin.Id };
        _approverAssign = new DocumentMasterAssignment { DocumentMasterId = _m1.Id, UserId = _goneRev.Id, AssignmentRole = AssignmentRole.Approver, AssignedByUserId = _admin.Id };
        _ownerAssign = new DocumentMasterAssignment { DocumentMasterId = _m1.Id, UserId = _gone.Id, AssignmentRole = AssignmentRole.Owner, AssignedByUserId = _admin.Id };
        _db.DocumentMasterAssignments.AddRange(_authorAssign, _reviewerAssign, _approverAssign, _ownerAssign,
            // An assignment held by an ACTIVE user, and one on a document without open revisions, are not queue items.
            new DocumentMasterAssignment { DocumentMasterId = _m1.Id, UserId = _candA.Id, AssignmentRole = AssignmentRole.Author, AssignedByUserId = _admin.Id },
            new DocumentMasterAssignment { DocumentMasterId = _mActive.Id, UserId = _gone.Id, AssignmentRole = AssignmentRole.Author, AssignedByUserId = _admin.Id });
        _reviewTask = new DocumentReviewTask { DocumentRevisionId = _r1.Id, AssignedReviewerUserId = _goneRev.Id, AssignedByUserId = _admin.Id, Status = ReviewTaskStatus.Pending };
        _approvalTask = new DocumentApprovalTask { DocumentRevisionId = _r1.Id, AssignedApproverUserId = _goneRev.Id, AssignedByUserId = _admin.Id, Status = DocumentApprovalTaskStatus.Pending };
        _periodic = new PeriodicReviewTask
        {
            DocumentMasterId = _m1.Id, DocumentRevisionId = _r1.Id, ReviewCycleMonths = 24, ScheduledDueDate = _clock.Now.UtcDateTime,
            AssignedReviewerUserId = _goneRev.Id, Status = PeriodicReviewTaskStatus.InProgress
        };
        _db.DocumentReviewTasks.AddRange(_reviewTask,
            // Finished work is not open.
            new DocumentReviewTask { DocumentRevisionId = _r1.Id, AssignedReviewerUserId = _goneRev.Id, AssignedByUserId = _admin.Id, Status = ReviewTaskStatus.Completed, ReviewCycleNumber = 2 });
        _db.DocumentApprovalTasks.AddRange(_approvalTask,
            new DocumentApprovalTask { DocumentRevisionId = _r1.Id, AssignedApproverUserId = _goneRev.Id, AssignedByUserId = _admin.Id, Status = DocumentApprovalTaskStatus.Approved });
        _db.PeriodicReviewTasks.AddRange(_periodic,
            new PeriodicReviewTask { DocumentMasterId = _m1.Id, DocumentRevisionId = _r1.Id, ReviewCycleMonths = 24, ScheduledDueDate = _clock.Now.UtcDateTime, AssignedReviewerUserId = _goneRev.Id, Status = PeriodicReviewTaskStatus.Completed },
            new PeriodicReviewTask { DocumentMasterId = _m1.Id, DocumentRevisionId = _r1.Id, ReviewCycleMonths = 24, ScheduledDueDate = _clock.Now.UtcDateTime, AssignedReviewerUserId = null, Status = PeriodicReviewTaskStatus.Pending });
        _db.SaveChanges();
    }

    private Task<ReassignmentQueueDto> QueueAsync(User by, int? department = null) => _svc.GetQueueAsync(department, by.Id);

    private Task<ReassignmentResultDto> ReassignAsync(string type, int itemId, User to, User by, string? reason = "Handover after resignation") =>
        _svc.ReassignAsync(new ReassignRequest(type, itemId, to.Id, reason), by.Id);

    // ---- queue contents -------------------------------------------------------------------------

    [Fact]
    public async Task Queue_ListsEachKindOfOpenResponsibilityHeldByAnInactiveUser()
    {
        var queue = await QueueAsync(_headA, _deptA);

        var byType = queue.Items.GroupBy(i => i.ItemType).ToDictionary(g => g.Key, g => g.ToList());
        Assert.Equal(7, queue.Total);
        Assert.Equal(_m1.Id, Assert.Single(byType[ReassignmentItemTypes.Custodian]).ItemId);
        Assert.Equal(3, byType[ReassignmentItemTypes.Assignment].Count);
        Assert.Equal(_reviewTask.Id, Assert.Single(byType[ReassignmentItemTypes.ReviewTask]).ItemId);
        Assert.Equal(_approvalTask.Id, Assert.Single(byType[ReassignmentItemTypes.ApprovalTask]).ItemId);
        Assert.Equal(_periodic.Id, Assert.Single(byType[ReassignmentItemTypes.PeriodicReview]).ItemId);

        var review = Assert.Single(byType[ReassignmentItemTypes.ReviewTask]);
        Assert.Equal("SOP-001", review.DocumentCode);
        Assert.Equal("02", review.RevisionNumber);
        Assert.Equal("Technical reviewer", review.RoleNeeded);
        Assert.Equal(PermissionConstants.DocumentsReview, review.RequiredPermission);
        Assert.Equal("Quality Control", review.DepartmentName);
        Assert.Equal(new[] { "Approver (assignment)", "Author (assignment)", "Technical reviewer (assignment)" },
            byType[ReassignmentItemTypes.Assignment].Select(i => i.RoleNeeded).OrderBy(r => r).ToArray());
        Assert.Equal(PermissionConstants.DocumentsDraftEdit, Assert.Single(byType[ReassignmentItemTypes.Custodian]).RequiredPermission);
    }

    [Fact]
    public async Task Queue_LeavesOutActiveHolders_VoidDocuments_FinishedWork_AndOtherDepartments()
    {
        var a = await QueueAsync(_headA, _deptA);
        Assert.DoesNotContain(a.Items, i => i.DocumentCode is "SOP-003" or "SOP-004" or "SOP-002");

        var b = await QueueAsync(_headB, _deptB);
        var only = Assert.Single(b.Items);
        Assert.Equal((ReassignmentItemTypes.Custodian, "SOP-002", "Production"), (only.ItemType, only.DocumentCode, only.DepartmentName));
    }

    [Fact]
    public async Task Queue_IsComputedFromTheData_SoItIsAlwaysCurrent()
    {
        Assert.Equal(7, (await QueueAsync(_headA, _deptA)).Total);

        // The user comes back: nothing needs reassigning, with no row to clean up.
        _gone.IsActive = true; _goneRev.IsActive = true;
        await _db.SaveChangesAsync();
        Assert.Equal(0, (await QueueAsync(_headA, _deptA)).Total);
    }

    [Fact]
    public async Task Queue_ShowsTheHolderAsInactiveUser_AndTheNameOnlyToTheQueueAudience()
    {
        var item = (await QueueAsync(_admin, _deptA)).Items.First(i => i.ItemType == ReassignmentItemTypes.Custodian);
        Assert.Equal("Inactive user", item.HolderLabel);
        Assert.Equal("gone", item.HolderName); // the person doing the hand-over needs to know whose work this was
        Assert.Equal(_gone.DeactivatedAt, item.InactiveSince);
    }

    [Fact]
    public async Task OrdinaryViewers_SeeInactiveHoldersWithoutTheirName()
    {
        var master = new DocumentMasterService(_db, new DatabaseSequenceHelper(_db), _audit, _auth);
        var dto = await master.GetByIdAsync(_m1.Id, _authorA.Id);

        Assert.Equal(DocumentDtoMapper.InactiveHolderLabel, dto.DocumentOwnerName);
        Assert.DoesNotContain("gone", dto.DocumentOwnerName);
        // Not hidden: an orphaned assignment must stay visible, just anonymous.
        var assigned = dto.Assignments.Where(a => a.UserId == _gone.Id || a.UserId == _goneRev.Id).ToList();
        Assert.NotEmpty(assigned);
        Assert.All(assigned, a =>
        {
            Assert.Equal(DocumentDtoMapper.InactiveHolderLabel, a.UserFullName);
            Assert.Equal("", a.UserName);
        });
        Assert.Contains(dto.Assignments, a => a.UserFullName == "candA"); // active holders keep their name
    }

    // ---- who may act -----------------------------------------------------------------------------

    [Fact]
    public async Task ConfigManageHolder_SeesAndCountsEveryDepartment()
    {
        var all = await QueueAsync(_admin);
        Assert.Equal(8, all.Total);
        Assert.Equal(8, (await _svc.GetCountAsync(_admin.Id)).Total);
        Assert.Equal(7, (await QueueAsync(_admin, _deptA)).Total);
    }

    [Fact]
    public async Task SectionHead_SeesOnlyTheDepartmentsTheyBelongTo()
    {
        Assert.Equal(7, (await QueueAsync(_headA)).Total);
        Assert.Equal(7, (await _svc.GetCountAsync(_headA.Id)).Total);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => QueueAsync(_headA, _deptB));
    }

    [Fact]
    public async Task SectionHead_WhoIsNotAMemberOfTheDepartment_CannotReassign_ByPermissionAlone()
    {
        // headB holds Documents.Reassign (the role) but belongs to the other department.
        Assert.True(await _auth.HoldsPermissionAsync(_headB.Id, PermissionConstants.DocumentsReassign));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReassignAsync(ReassignmentItemTypes.Custodian, _m1.Id, _candA, _headB));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _svc.GetCandidatesAsync(ReassignmentItemTypes.Custodian, _m1.Id, _headB.Id));
        Assert.Equal(_gone.Id, (await _db.DocumentMasters.FindAsync(_m1.Id))!.DocumentOwnerUserId);
    }

    [Fact]
    public async Task UsersWithoutEitherPermission_AreRefused()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => QueueAsync(_authorA));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _svc.GetCountAsync(_revA.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReassignAsync(ReassignmentItemTypes.Custodian, _m1.Id, _candA, _authorA));
    }

    [Fact]
    public async Task AuthorityComesFromThePermission_NotTheRoleName()
    {
        // Withdraw the grant from the Section Head role: the role name no longer matters.
        var rows = await _db.RolePermissions.Where(rp => rp.RoleId == _headRole.Id && rp.Permission!.Code == PermissionConstants.DocumentsReassign).ToListAsync();
        Assert.Single(rows);
        _db.RolePermissions.RemoveRange(rows);
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => QueueAsync(_headA, _deptA));
    }

    [Fact]
    public async Task Candidates_AreActiveMembersOfTheDepartmentHoldingTheCodeAndPassingSegregationOfDuties()
    {
        var custodian = await _svc.GetCandidatesAsync(ReassignmentItemTypes.Custodian, _m1.Id, _headA.Id);
        // DraftEdit: analysts and section heads in dept A. Not the inactive holders, not dept B's people.
        Assert.Equal(new[] { "authorA", "candA", "headA" }, custodian.Select(c => c.FullName).OrderBy(n => n).ToArray());

        var reviewers = await _svc.GetCandidatesAsync(ReassignmentItemTypes.ReviewTask, _reviewTask.Id, _headA.Id);
        Assert.DoesNotContain(reviewers, c => c.FullName is "outsider" or "authorA" or "goneRev");
        Assert.Contains(reviewers, c => c.FullName == "revA");
    }

    // ---- the rules of a hand-over ---------------------------------------------------------------

    [Fact]
    public async Task Reassign_RequiresAReason()
    {
        foreach (var reason in new string?[] { null, "", "   " })
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ReassignAsync(ReassignmentItemTypes.Custodian, _m1.Id, _candA, _headA, reason));
            Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(_gone.Id, (await _db.DocumentMasters.FindAsync(_m1.Id))!.DocumentOwnerUserId);
        Assert.Empty(_db.NotificationLogs);
    }

    [Fact]
    public async Task Reassign_RefusesAnInactiveTarget()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ReassignAsync(ReassignmentItemTypes.Custodian, _m1.Id, _goneRev, _headA));
        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public async Task Reassign_RefusesAUserWhoIsNotAMemberOfTheDocumentsDepartment()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ReassignAsync(ReassignmentItemTypes.ReviewTask, _reviewTask.Id, _outsider, _headA));
        Assert.Contains("not a member", ex.Message);
        Assert.Equal(_goneRev.Id, (await _db.DocumentReviewTasks.FindAsync(_reviewTask.Id))!.AssignedReviewerUserId);
    }

    [Theory]
    [InlineData(ReassignmentItemTypes.ReviewTask, PermissionConstants.DocumentsReview)]
    [InlineData(ReassignmentItemTypes.ApprovalTask, PermissionConstants.DocumentsApprove)]
    [InlineData(ReassignmentItemTypes.PeriodicReview, PermissionConstants.DocumentsPeriodicReview)]
    [InlineData(ReassignmentItemTypes.Custodian, PermissionConstants.DocumentsDraftEdit)]
    public async Task Reassign_RefusesAUserWhoseRoleLacksTheCodeTheRoleNeeds(string type, string code)
    {
        // candA is an Analyst: DraftEdit yes; Review, Approve and PeriodicReview no. Take the code away to cover the custodian case.
        var rows = await _db.RolePermissions.Where(rp => rp.RoleId == _analystRole.Id && rp.Permission!.Code == code).ToListAsync();
        _db.RolePermissions.RemoveRange(rows);
        await _db.SaveChangesAsync();
        var id = type switch
        {
            ReassignmentItemTypes.ReviewTask => _reviewTask.Id,
            ReassignmentItemTypes.ApprovalTask => _approvalTask.Id,
            ReassignmentItemTypes.PeriodicReview => _periodic.Id,
            _ => _m1.Id
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ReassignAsync(type, id, _candA, _headA));
        Assert.Contains(code, ex.Message);
    }

    [Fact]
    public async Task Reassign_AppliesSegregationOfDuties()
    {
        // The revision's author cannot take over its technical review or periodic review.
        _r1.CreatedByUserId = _rev2A.Id;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReassignAsync(ReassignmentItemTypes.ReviewTask, _reviewTask.Id, _rev2A, _headA));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReassignAsync(ReassignmentItemTypes.PeriodicReview, _periodic.Id, _rev2A, _headA));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReassignAsync(ReassignmentItemTypes.ApprovalTask, _approvalTask.Id, _rev2A, _headA));

        // A technical reviewer of the revision cannot become its approver.
        _r1.CreatedByUserId = _authorA.Id;
        _db.DocumentReviewTasks.Add(new DocumentReviewTask
        {
            DocumentRevisionId = _r1.Id, AssignedReviewerUserId = _revA.Id, DecisionByUserId = _revA.Id, AssignedByUserId = _admin.Id,
            Status = ReviewTaskStatus.Completed, ReviewCycleNumber = 5
        });
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ReassignAsync(ReassignmentItemTypes.ApprovalTask, _approvalTask.Id, _revA, _headA));
        await ReassignAsync(ReassignmentItemTypes.ApprovalTask, _approvalTask.Id, _rev2A, _headA);
    }

    [Fact]
    public async Task Reassign_OnlyMovesResponsibilitiesHeldByInactiveUsers()
    {
        _goneRev.IsActive = true;
        await _db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ReassignAsync(ReassignmentItemTypes.ReviewTask, _reviewTask.Id, _revA, _headA));
        Assert.Contains("active user", ex.Message);
    }

    [Fact]
    public async Task Reassign_RejectsAnUnknownItemType()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.ReassignAsync(new ReassignRequest("Bogus", 1, _candA.Id, "r"), _headA.Id));
    }

    // ---- a valid hand-over ------------------------------------------------------------------------

    [Fact]
    public async Task Custodian_IsReassigned_Audited_Notified_AndLeavesTheQueue()
    {
        var result = await ReassignAsync(ReassignmentItemTypes.Custodian, _m1.Id, _candA, _headA);

        Assert.Equal((_gone.Id, _candA.Id), (result.PreviousUserId, result.NewUserId));
        var master = await _db.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == _m1.Id);
        Assert.Equal(_candA.Id, master.DocumentOwnerUserId);
        Assert.Equal(_headA.Id, master.ModifiedByUserId);
        // The Owner-role assignment follows: the old one is closed (kept as history), a new one opened.
        Assert.False((await _db.DocumentMasterAssignments.AsNoTracking().SingleAsync(a => a.Id == _ownerAssign.Id)).IsActive);
        Assert.True(await _db.DocumentMasterAssignments.AnyAsync(a => a.DocumentMasterId == _m1.Id && a.UserId == _candA.Id && a.AssignmentRole == AssignmentRole.Owner && a.IsActive));

        var audit = await _db.AuditLogs.Include(a => a.Changes).AsNoTracking().SingleAsync(a => a.ActionCode == "DocumentCustodianReassigned");
        Assert.Equal(AuditActionCategory.Document, audit.ActionCategory);
        Assert.Equal("Handover after resignation", audit.Reason);
        Assert.Equal(_headA.Id, audit.UserId);
        var change = Assert.Single(audit.Changes, c => c.FieldName == "DocumentOwnerUserId");
        Assert.Equal((_gone.Id.ToString(), _candA.Id.ToString()), (change.PreviousValue, change.NewValue));

        var note = await _db.NotificationLogs.AsNoTracking().SingleAsync();
        Assert.Equal((_candA.Id, DocumentReassignmentService.NotificationType), (note.UserId, note.Type));
        Assert.Contains("SOP-001", note.Message);
        Assert.Equal(_candA.Id, Assert.Single(_push.Sent).UserId);

        Assert.DoesNotContain((await QueueAsync(_headA, _deptA)).Items, i => i.ItemType == ReassignmentItemTypes.Custodian);
    }

    [Fact]
    public async Task Assignment_OldRowIsClosedAndANewOneOpened()
    {
        await ReassignAsync(ReassignmentItemTypes.Assignment, _authorAssign.Id, _candA, _headA);

        Assert.False((await _db.DocumentMasterAssignments.AsNoTracking().SingleAsync(a => a.Id == _authorAssign.Id)).IsActive);
        var fresh = await _db.DocumentMasterAssignments.AsNoTracking().Where(a => a.DocumentMasterId == _m1.Id && a.UserId == _candA.Id && a.AssignmentRole == AssignmentRole.Author).ToListAsync();
        Assert.True(Assert.Single(fresh).IsActive); // candA already had this assignment: it stays the single active one
        Assert.Equal("DocumentAssignmentReassigned", (await _db.AuditLogs.AsNoTracking().SingleAsync(a => a.ActionCode == "DocumentAssignmentReassigned")).ActionCode);
    }

    [Fact]
    public async Task ReviewTask_IsReassigned_WithReviewCategoryAudit()
    {
        await ReassignAsync(ReassignmentItemTypes.ReviewTask, _reviewTask.Id, _revA, _headA);

        Assert.Equal(_revA.Id, (await _db.DocumentReviewTasks.AsNoTracking().SingleAsync(t => t.Id == _reviewTask.Id)).AssignedReviewerUserId);
        var audit = await _db.AuditLogs.AsNoTracking().SingleAsync(a => a.ActionCode == "ReviewTaskReassigned");
        Assert.Equal(AuditActionCategory.Review, audit.ActionCategory);
        Assert.Equal(_r1.Id, audit.DocumentRevisionId);
        Assert.Equal(_revA.Id, (await _db.NotificationLogs.AsNoTracking().SingleAsync()).UserId);
    }

    [Fact]
    public async Task ApprovalTask_IsReassigned_WithApprovalCategoryAudit()
    {
        await ReassignAsync(ReassignmentItemTypes.ApprovalTask, _approvalTask.Id, _revA, _admin);

        Assert.Equal(_revA.Id, (await _db.DocumentApprovalTasks.AsNoTracking().SingleAsync(t => t.Id == _approvalTask.Id)).AssignedApproverUserId);
        Assert.Equal(AuditActionCategory.Approval, (await _db.AuditLogs.AsNoTracking().SingleAsync(a => a.ActionCode == "ApprovalTaskReassigned")).ActionCategory);
    }

    [Fact]
    public async Task PeriodicReview_IsReassigned()
    {
        await ReassignAsync(ReassignmentItemTypes.PeriodicReview, _periodic.Id, _rev2A, _headA);

        Assert.Equal(_rev2A.Id, (await _db.PeriodicReviewTasks.AsNoTracking().SingleAsync(t => t.Id == _periodic.Id)).AssignedReviewerUserId);
        Assert.Equal("PeriodicReviewReassigned", (await _db.AuditLogs.AsNoTracking().SingleAsync(a => a.ActionCode == "PeriodicReviewReassigned")).ActionCode);
    }

    [Fact]
    public async Task Reassign_ByAConfigManageHolder_NeedsNoMembership()
    {
        // admin belongs to no department; headB is a member of the document's department B and holds DraftEdit.
        await ReassignAsync(ReassignmentItemTypes.Custodian, _m2.Id, _headB, _admin);
        Assert.Equal(_headB.Id, (await _db.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == _m2.Id)).DocumentOwnerUserId);
    }

    // ---- escalation -------------------------------------------------------------------------------

    [Fact]
    public async Task Items_AreFlaggedOverdue_FromTheHoldersDeactivationTime()
    {
        var queue = await QueueAsync(_headA, _deptA);

        Assert.Equal(7, queue.Total);
        Assert.Equal(7, queue.EscalationDays); // default
        // gone was deactivated 10 days ago (overdue), goneRev 2 days ago (not).
        Assert.All(queue.Items.Where(i => i.HolderName == "gone"), i => Assert.True(i.IsOverdue));
        Assert.All(queue.Items.Where(i => i.HolderName == "goneRev"), i => Assert.False(i.IsOverdue));
        Assert.Equal(2, queue.Overdue); // gone: the custodianship and the author assignment
        Assert.True(queue.Items[0].IsOverdue); // overdue items come first
    }

    [Fact]
    public async Task EscalationDays_ComesFromTheSettingsRegistry()
    {
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = SystemSettingKeys.ReassignmentEscalationDays, Value = "30", ValueType = SystemSettingValueType.Integer,
            Category = "Document Control", Description = "d", CurrentVersion = 1
        });
        await _db.SaveChangesAsync();
        var svc = NewService(new SystemSettingsReader(_db, new SystemSettingsCache(), timeProvider: _clock));

        var queue = await svc.GetQueueAsync(_deptA, _headA.Id);
        Assert.Equal(30, queue.EscalationDays);
        Assert.Equal(0, queue.Overdue);
        Assert.Equal(0, await svc.NotifyOverdueAsync());
    }

    [Fact]
    public async Task ItemsWithoutARecordedDeactivationTime_AreNeverFlagged()
    {
        _gone.DeactivatedAt = null;
        await _db.SaveChangesAsync();
        Assert.Equal(0, (await QueueAsync(_headA, _deptA)).Overdue);
    }

    [Fact]
    public async Task NotifyOverdue_TellsEachDepartmentsSectionHeads_OnceADay_AndNeverReassigns()
    {
        var created = await _svc.NotifyOverdueAsync();

        // Dept A (custodian + author assignment held by "gone"; goneRev's items are still young) -> headA;
        // dept B (custodian held by "gone") -> headB. Nobody else.
        Assert.Equal(2, created);
        var notes = await _db.NotificationLogs.AsNoTracking().ToListAsync();
        Assert.All(notes, n => Assert.Equal(DocumentReassignmentService.OverdueNotificationType, n.Type));
        Assert.Equal(new[] { _headA.Id, _headB.Id }.OrderBy(i => i), notes.Select(n => n.UserId).OrderBy(i => i));
        Assert.Contains("[Quality Control] 2 document responsibilities", notes.Single(n => n.UserId == _headA.Id).Message);
        Assert.Contains("[Production] 1 document responsibility", notes.Single(n => n.UserId == _headB.Id).Message);

        // Nothing was moved.
        Assert.Equal(_gone.Id, (await _db.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == _m1.Id)).DocumentOwnerUserId);
        Assert.Equal(_goneRev.Id, (await _db.DocumentReviewTasks.AsNoTracking().SingleAsync(t => t.Id == _reviewTask.Id)).AssignedReviewerUserId);

        // Hourly worker: no repeat within the day, a reminder the next day.
        Assert.Equal(0, await _svc.NotifyOverdueAsync());
        _clock.Now = _clock.Now.AddHours(25);
        Assert.Equal(2, await _svc.NotifyOverdueAsync());
    }

    [Fact]
    public async Task NotifyOverdue_StaysQuiet_WhenNothingIsOverdue()
    {
        _gone.DeactivatedAt = _clock.Now.UtcDateTime.AddDays(-1);
        await _db.SaveChangesAsync();
        Assert.Equal(0, await _svc.NotifyOverdueAsync());
        Assert.Empty(_db.NotificationLogs);
    }

    [Fact]
    public void EscalationSetting_IsRegistered_WithDefaultSeven_AndNeedsNoSignature()
    {
        var definition = SystemSettingDefinitions.Get(SystemSettingKeys.ReassignmentEscalationDays);
        Assert.Equal("DocumentControl.ReassignmentEscalationDays", definition.Key);
        Assert.Equal("7", definition.Default);
        Assert.False(definition.IsGxpRelevant); // reason only, not e-signed
    }

    [Fact]
    public void ReassignPermission_IsEnforced_InTheCatalog_GrantedToSectionHeadsOnly_AndNotAnSodCode()
    {
        Assert.Contains(PermissionConstants.DocumentsReassign, PermissionConstants.All);
        Assert.Contains(PermissionConstants.DocumentsReassign, PermissionConstants.Enforced);
        Assert.DoesNotContain(PermissionConstants.DocumentsReassign, PermissionConstants.SystemAdministratorGrants);
        var granted = _db.RolePermissions.Where(rp => rp.Permission!.Code == PermissionConstants.DocumentsReassign).Select(rp => rp.RoleId).ToList();
        Assert.Equal(_headRole.Id, Assert.Single(granted));
    }
}
