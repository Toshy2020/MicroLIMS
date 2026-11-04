using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Notifications;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.DocumentControl;

// Succession for deactivated users (owner decision): documents belong to their department, with a named
// custodian. Deactivating a user does not move anything. Their open responsibilities are found by query
// (the holder is inactive), shown per department, and handed to another active member of that department
// by a person with authority, who gives a reason. Each hand-over is audited and the new holder is notified.
public class DocumentReassignmentService : IDocumentReassignmentService
{
    public const string NotificationType = "DocumentReassigned";
    public const string OverdueNotificationType = "ReassignmentOverdue";
    private static readonly TimeSpan OverdueNotificationDedupe = TimeSpan.FromHours(24);

    // Revisions that are still moving through the workflow. An assignment on a document with none of these
    // is not open work; it is picked up when the next revision starts.
    private static readonly DocumentRevisionStatus[] OpenRevisionStatuses =
    {
        DocumentRevisionStatus.Draft, DocumentRevisionStatus.InReview, DocumentRevisionStatus.AwaitingApproval
    };

    private readonly IMicroLimsDbContext _db;
    private readonly IAuditEventService _audit;
    private readonly IDocumentAuthorizationService _auth;
    private readonly INotificationService? _push;
    private readonly ISystemSettingsReader? _settings;
    private readonly TimeProvider _time;

    public DocumentReassignmentService(
        IMicroLimsDbContext db,
        IAuditEventService audit,
        IDocumentAuthorizationService auth,
        INotificationService? push = null,
        ISystemSettingsReader? settings = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _audit = audit;
        _auth = auth;
        _push = push;
        _settings = settings;
        _time = timeProvider ?? TimeProvider.System;
    }

    // ---- who may act ---------------------------------------------------------------------------

    // Permission plus membership, never a role name: Documents.ConfigManage covers every department; Documents.Reassign
    // (the Section Head) covers the departments the user is a member of.
    private sealed record Scope(bool AllDepartments, HashSet<int> DepartmentIds)
    {
        public bool Covers(int departmentId) => AllDepartments || DepartmentIds.Contains(departmentId);
    }

    private async Task<Scope?> TryGetScopeAsync(int userId)
    {
        if (await _auth.HoldsPermissionAsync(userId, PermissionConstants.DocumentsConfigManage))
            return new Scope(true, new HashSet<int>());
        if (await _auth.HoldsPermissionAsync(userId, PermissionConstants.DocumentsReassign))
        {
            var departments = await _db.UserOrgMemberships.AsNoTracking()
                .Where(m => m.UserId == userId).Select(m => m.DepartmentId).Distinct().ToListAsync();
            return new Scope(false, departments.ToHashSet());
        }
        return null;
    }

    private async Task<Scope> GetScopeAsync(int userId) =>
        await TryGetScopeAsync(userId)
        ?? throw new UnauthorizedAccessException("Reassigning document responsibilities needs the Documents.Reassign or Documents.ConfigManage permission.");

    private static void EnsureCovers(Scope scope, int departmentId)
    {
        if (!scope.Covers(departmentId))
            throw new UnauthorizedAccessException("You can only reassign responsibilities for a department you are a member of.");
    }

    // ---- the queue -----------------------------------------------------------------------------

    private sealed class Raw
    {
        public string ItemType { get; init; } = "";
        public int ItemId { get; init; }
        public int DepartmentId { get; init; }
        public string DepartmentName { get; init; } = "";
        public int MasterId { get; init; }
        public string Code { get; init; } = "";
        public string Title { get; init; } = "";
        public string? RevisionNumber { get; init; }
        public AssignmentRole? AssignmentRole { get; init; }
        public string HolderName { get; init; } = "";
        public DateTime? DeactivatedAt { get; init; }
    }

    private async Task<List<Raw>> LoadAsync(IReadOnlyCollection<int>? departments)
    {
        var items = new List<Raw>();

        var owners = _db.DocumentMasters.AsNoTracking()
            .Where(m => m.RecordStatus == DocumentRecordStatus.Active && !m.DocumentOwnerUser.IsActive);
        if (departments is not null) owners = owners.Where(m => departments.Contains(m.DepartmentId));
        items.AddRange(await owners.Select(m => new Raw
        {
            ItemType = ReassignmentItemTypes.Custodian, ItemId = m.Id, MasterId = m.Id,
            DepartmentId = m.DepartmentId, DepartmentName = m.Department.Name,
            Code = m.CompanyDocumentCode, Title = m.Title,
            RevisionNumber = m.Revisions.OrderByDescending(r => r.RevisionSequence).Select(r => r.RevisionNumber).FirstOrDefault(),
            HolderName = m.DocumentOwnerUser.FullName, DeactivatedAt = m.DocumentOwnerUser.DeactivatedAt
        }).ToListAsync());

        var assignments = _db.DocumentMasterAssignments.AsNoTracking()
            .Where(a => a.IsActive && a.AssignmentRole != AssignmentRole.Owner && !a.User.IsActive
                        && a.DocumentMaster.RecordStatus == DocumentRecordStatus.Active
                        && a.DocumentMaster.Revisions.Any(r => OpenRevisionStatuses.Contains(r.RevisionStatus)));
        if (departments is not null) assignments = assignments.Where(a => departments.Contains(a.DocumentMaster.DepartmentId));
        items.AddRange(await assignments.Select(a => new Raw
        {
            ItemType = ReassignmentItemTypes.Assignment, ItemId = a.Id, MasterId = a.DocumentMasterId,
            DepartmentId = a.DocumentMaster.DepartmentId, DepartmentName = a.DocumentMaster.Department.Name,
            Code = a.DocumentMaster.CompanyDocumentCode, Title = a.DocumentMaster.Title,
            RevisionNumber = a.DocumentMaster.Revisions.Where(r => OpenRevisionStatuses.Contains(r.RevisionStatus))
                .OrderByDescending(r => r.RevisionSequence).Select(r => r.RevisionNumber).FirstOrDefault(),
            AssignmentRole = a.AssignmentRole,
            HolderName = a.User.FullName, DeactivatedAt = a.User.DeactivatedAt
        }).ToListAsync());

        var reviews = _db.DocumentReviewTasks.AsNoTracking()
            .Where(t => (t.Status == ReviewTaskStatus.Pending || t.Status == ReviewTaskStatus.InProgress)
                        && !t.AssignedReviewerUser.IsActive
                        && t.DocumentRevision.DocumentMaster.RecordStatus == DocumentRecordStatus.Active);
        if (departments is not null) reviews = reviews.Where(t => departments.Contains(t.DocumentRevision.DocumentMaster.DepartmentId));
        items.AddRange(await reviews.Select(t => new Raw
        {
            ItemType = ReassignmentItemTypes.ReviewTask, ItemId = t.Id, MasterId = t.DocumentRevision.DocumentMasterId,
            DepartmentId = t.DocumentRevision.DocumentMaster.DepartmentId, DepartmentName = t.DocumentRevision.DocumentMaster.Department.Name,
            Code = t.DocumentRevision.DocumentMaster.CompanyDocumentCode, Title = t.DocumentRevision.DocumentMaster.Title,
            RevisionNumber = t.DocumentRevision.RevisionNumber,
            HolderName = t.AssignedReviewerUser.FullName, DeactivatedAt = t.AssignedReviewerUser.DeactivatedAt
        }).ToListAsync());

        var approvals = _db.DocumentApprovalTasks.AsNoTracking()
            .Where(t => t.Status == DocumentApprovalTaskStatus.Pending
                        && !t.AssignedApproverUser.IsActive
                        && t.DocumentRevision.DocumentMaster.RecordStatus == DocumentRecordStatus.Active);
        if (departments is not null) approvals = approvals.Where(t => departments.Contains(t.DocumentRevision.DocumentMaster.DepartmentId));
        items.AddRange(await approvals.Select(t => new Raw
        {
            ItemType = ReassignmentItemTypes.ApprovalTask, ItemId = t.Id, MasterId = t.DocumentRevision.DocumentMasterId,
            DepartmentId = t.DocumentRevision.DocumentMaster.DepartmentId, DepartmentName = t.DocumentRevision.DocumentMaster.Department.Name,
            Code = t.DocumentRevision.DocumentMaster.CompanyDocumentCode, Title = t.DocumentRevision.DocumentMaster.Title,
            RevisionNumber = t.DocumentRevision.RevisionNumber,
            HolderName = t.AssignedApproverUser.FullName, DeactivatedAt = t.AssignedApproverUser.DeactivatedAt
        }).ToListAsync());

        var periodic = _db.PeriodicReviewTasks.AsNoTracking()
            .Where(t => (t.Status == PeriodicReviewTaskStatus.Pending || t.Status == PeriodicReviewTaskStatus.InProgress)
                        && t.AssignedReviewerUserId != null && !t.AssignedReviewerUser!.IsActive
                        && t.DocumentMaster.RecordStatus == DocumentRecordStatus.Active);
        if (departments is not null) periodic = periodic.Where(t => departments.Contains(t.DocumentMaster.DepartmentId));
        items.AddRange(await periodic.Select(t => new Raw
        {
            ItemType = ReassignmentItemTypes.PeriodicReview, ItemId = t.Id, MasterId = t.DocumentMasterId,
            DepartmentId = t.DocumentMaster.DepartmentId, DepartmentName = t.DocumentMaster.Department.Name,
            Code = t.DocumentMaster.CompanyDocumentCode, Title = t.DocumentMaster.Title,
            RevisionNumber = t.DocumentRevision.RevisionNumber,
            HolderName = t.AssignedReviewerUser!.FullName, DeactivatedAt = t.AssignedReviewerUser!.DeactivatedAt
        }).ToListAsync());

        return items;
    }

    private static (string Role, string Permission) RoleFor(string itemType, AssignmentRole? assignmentRole) => itemType switch
    {
        ReassignmentItemTypes.Custodian => ("Custodian", PermissionConstants.DocumentsDraftEdit),
        ReassignmentItemTypes.ReviewTask => ("Technical reviewer", PermissionConstants.DocumentsReview),
        ReassignmentItemTypes.ApprovalTask => ("Approver", PermissionConstants.DocumentsApprove),
        ReassignmentItemTypes.PeriodicReview => ("Periodic reviewer", PermissionConstants.DocumentsPeriodicReview),
        _ => assignmentRole switch
        {
            AssignmentRole.TechnicalReviewer => ("Technical reviewer (assignment)", PermissionConstants.DocumentsReview),
            AssignmentRole.Approver => ("Approver (assignment)", PermissionConstants.DocumentsApprove),
            _ => ("Author (assignment)", PermissionConstants.DocumentsDraftEdit)
        }
    };

    private async Task<int> EscalationDaysAsync() =>
        await _settings.GetIntOrDefaultAsync(SystemSettingKeys.ReassignmentEscalationDays);

    private static bool IsOverdue(DateTime? deactivatedAt, DateTime now, int days) =>
        deactivatedAt is { } at && at <= now.AddDays(-days);

    private async Task<List<ReassignmentItemDto>> BuildItemsAsync(IReadOnlyCollection<int>? departments)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var days = await EscalationDaysAsync();
        return (await LoadAsync(departments))
            .Select(r =>
            {
                var (role, permission) = RoleFor(r.ItemType, r.AssignmentRole);
                return new ReassignmentItemDto(
                    r.ItemType, r.ItemId, r.DepartmentId, r.DepartmentName, r.MasterId, r.Code, r.Title, r.RevisionNumber,
                    role, permission, "Inactive user", r.HolderName, r.DeactivatedAt, IsOverdue(r.DeactivatedAt, now, days));
            })
            .OrderByDescending(i => i.IsOverdue)
            .ThenBy(i => i.InactiveSince ?? DateTime.MaxValue)
            .ThenBy(i => i.DepartmentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.DocumentCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.ItemType, StringComparer.Ordinal)
            .ThenBy(i => i.ItemId)
            .ToList();
    }

    private static IReadOnlyCollection<int>? DepartmentFilter(Scope scope, int? departmentId)
    {
        if (departmentId is { } id)
        {
            EnsureCovers(scope, id);
            return new[] { id };
        }
        return scope.AllDepartments ? null : scope.DepartmentIds.ToList();
    }

    public async Task<ReassignmentQueueDto> GetQueueAsync(int? departmentId, int userId)
    {
        var scope = await GetScopeAsync(userId);
        var items = await BuildItemsAsync(DepartmentFilter(scope, departmentId));
        return new ReassignmentQueueDto(items, items.Count, items.Count(i => i.IsOverdue), await EscalationDaysAsync());
    }

    public async Task<ReassignmentCountDto> GetCountAsync(int userId) =>
        await TryGetCountAsync(userId)
        ?? throw new UnauthorizedAccessException("Reassigning document responsibilities needs the Documents.Reassign or Documents.ConfigManage permission.");

    public async Task<ReassignmentCountDto?> TryGetCountAsync(int userId)
    {
        var scope = await TryGetScopeAsync(userId);
        if (scope is null) return null;
        var items = await BuildItemsAsync(DepartmentFilter(scope, null));
        return new ReassignmentCountDto(items.Count, items.Count(i => i.IsOverdue));
    }

    // ---- resolving one item --------------------------------------------------------------------

    // One open responsibility loaded for change: who holds it, what the new holder must be, the people who
    // may not take it (segregation of duties) and how to move it.
    private sealed class Target
    {
        public string ItemType { get; init; } = "";
        public int ItemId { get; init; }
        public int MasterId { get; init; }
        public int? RevisionId { get; init; }
        public int DepartmentId { get; init; }
        public string DepartmentName { get; init; } = "";
        public string Code { get; init; } = "";
        public string Title { get; init; } = "";
        public int HolderUserId { get; init; }
        public string Role { get; init; } = "";
        public string Permission { get; init; } = "";
        public HashSet<int> Excluded { get; init; } = new();
        public AuditActionCategory Category { get; init; }
        public string ActionCode { get; init; } = "";
        public string RecordType { get; init; } = "";
        public required Func<int, DateTime, Task<List<AuditFieldChange>>> Apply { get; init; }
    }

    private async Task<Target> ResolveAsync(string? itemType, int itemId)
    {
        switch (itemType)
        {
            case ReassignmentItemTypes.Custodian:
            {
                var m = await _db.DocumentMasters.Include(x => x.Department).Include(x => x.DocumentOwnerUser)
                            .FirstOrDefaultAsync(x => x.Id == itemId && x.RecordStatus == DocumentRecordStatus.Active)
                        ?? throw new KeyNotFoundException($"Document {itemId} was not found or is not active.");
                EnsureHolderInactive(m.DocumentOwnerUser);
                return new Target
                {
                    ItemType = itemType, ItemId = itemId, MasterId = m.Id, DepartmentId = m.DepartmentId, DepartmentName = m.Department.Name,
                    Code = m.CompanyDocumentCode, Title = m.Title, HolderUserId = m.DocumentOwnerUserId,
                    Role = "Custodian", Permission = PermissionConstants.DocumentsDraftEdit,
                    Category = AuditActionCategory.Document, ActionCode = "DocumentCustodianReassigned", RecordType = nameof(DocumentMaster),
                    Apply = async (newUserId, now) =>
                    {
                        var changes = new List<AuditFieldChange> { new("DocumentOwnerUserId", m.DocumentOwnerUserId.ToString(), newUserId.ToString()) };
                        var previous = m.DocumentOwnerUserId;
                        m.DocumentOwnerUserId = newUserId;
                        m.ModifiedAt = now;
                        m.ModifiedByUserId = _actingUserId;
                        // An Owner-role assignment made at registration follows the custodianship.
                        var ownerRows = await _db.DocumentMasterAssignments
                            .Where(a => a.DocumentMasterId == m.Id && a.AssignmentRole == AssignmentRole.Owner && a.IsActive && a.UserId == previous)
                            .ToListAsync();
                        if (ownerRows.Count > 0)
                        {
                            foreach (var row in ownerRows) row.IsActive = false;
                            await GrantAssignmentAsync(m.Id, newUserId, AssignmentRole.Owner, now);
                            changes.Add(new("OwnerAssignment", previous.ToString(), newUserId.ToString()));
                        }
                        return changes;
                    }
                };
            }
            case ReassignmentItemTypes.Assignment:
            {
                var a = await _db.DocumentMasterAssignments
                            .Include(x => x.User).Include(x => x.DocumentMaster).ThenInclude(x => x.Department)
                            .FirstOrDefaultAsync(x => x.Id == itemId && x.IsActive && x.AssignmentRole != AssignmentRole.Owner)
                        ?? throw new KeyNotFoundException($"Assignment {itemId} was not found or is no longer active.");
                EnsureHolderInactive(a.User);
                var (role, permission) = RoleFor(itemType, a.AssignmentRole);
                var m = a.DocumentMaster;
                return new Target
                {
                    ItemType = itemType, ItemId = itemId, MasterId = m.Id, DepartmentId = m.DepartmentId, DepartmentName = m.Department.Name,
                    Code = m.CompanyDocumentCode, Title = m.Title, HolderUserId = a.UserId, Role = role, Permission = permission,
                    Category = AuditActionCategory.Document, ActionCode = "DocumentAssignmentReassigned", RecordType = nameof(DocumentMasterAssignment),
                    Apply = async (newUserId, now) =>
                    {
                        var previous = a.UserId;
                        // The old assignment is closed, not edited: it stays as the record of who held the role.
                        a.IsActive = false;
                        await GrantAssignmentAsync(m.Id, newUserId, a.AssignmentRole, now);
                        return new List<AuditFieldChange>
                        {
                            new("AssignmentRole", a.AssignmentRole.ToString(), a.AssignmentRole.ToString()),
                            new("AssignedUserId", previous.ToString(), newUserId.ToString()),
                            new("IsActive", "true", "false")
                        };
                    }
                };
            }
            case ReassignmentItemTypes.ReviewTask:
            {
                var t = await _db.DocumentReviewTasks
                            .Include(x => x.AssignedReviewerUser)
                            .Include(x => x.DocumentRevision).ThenInclude(r => r.DocumentMaster).ThenInclude(m => m.Department)
                            .FirstOrDefaultAsync(x => x.Id == itemId && (x.Status == ReviewTaskStatus.Pending || x.Status == ReviewTaskStatus.InProgress))
                        ?? throw new KeyNotFoundException($"Review task {itemId} was not found or is no longer open.");
                EnsureHolderInactive(t.AssignedReviewerUser);
                var r = t.DocumentRevision;
                return new Target
                {
                    ItemType = itemType, ItemId = itemId, MasterId = r.DocumentMasterId, RevisionId = r.Id,
                    DepartmentId = r.DocumentMaster.DepartmentId, DepartmentName = r.DocumentMaster.Department.Name,
                    Code = r.DocumentMaster.CompanyDocumentCode, Title = r.DocumentMaster.Title, HolderUserId = t.AssignedReviewerUserId,
                    Role = "Technical reviewer", Permission = PermissionConstants.DocumentsReview,
                    Excluded = new HashSet<int> { r.CreatedByUserId },
                    Category = AuditActionCategory.Review, ActionCode = "ReviewTaskReassigned", RecordType = nameof(DocumentReviewTask),
                    Apply = (newUserId, _) =>
                    {
                        var changes = new List<AuditFieldChange> { new("AssignedReviewerUserId", t.AssignedReviewerUserId.ToString(), newUserId.ToString()) };
                        t.AssignedReviewerUserId = newUserId;
                        return Task.FromResult(changes);
                    }
                };
            }
            case ReassignmentItemTypes.ApprovalTask:
            {
                var t = await _db.DocumentApprovalTasks
                            .Include(x => x.AssignedApproverUser)
                            .Include(x => x.DocumentRevision).ThenInclude(r => r.ReviewTasks)
                            .Include(x => x.DocumentRevision).ThenInclude(r => r.DocumentMaster).ThenInclude(m => m.Department)
                            .FirstOrDefaultAsync(x => x.Id == itemId && x.Status == DocumentApprovalTaskStatus.Pending)
                        ?? throw new KeyNotFoundException($"Approval task {itemId} was not found or is no longer pending.");
                EnsureHolderInactive(t.AssignedApproverUser);
                var r = t.DocumentRevision;
                // Author != approver and technical reviewer != approver (DC-URS-078, DC-URS-164..169).
                var excluded = new HashSet<int> { r.CreatedByUserId };
                foreach (var rt in r.ReviewTasks)
                {
                    excluded.Add(rt.AssignedReviewerUserId);
                    if (rt.DecisionByUserId is { } decided) excluded.Add(decided);
                }
                return new Target
                {
                    ItemType = itemType, ItemId = itemId, MasterId = r.DocumentMasterId, RevisionId = r.Id,
                    DepartmentId = r.DocumentMaster.DepartmentId, DepartmentName = r.DocumentMaster.Department.Name,
                    Code = r.DocumentMaster.CompanyDocumentCode, Title = r.DocumentMaster.Title, HolderUserId = t.AssignedApproverUserId,
                    Role = "Approver", Permission = PermissionConstants.DocumentsApprove, Excluded = excluded,
                    Category = AuditActionCategory.Approval, ActionCode = "ApprovalTaskReassigned", RecordType = nameof(DocumentApprovalTask),
                    Apply = (newUserId, _) =>
                    {
                        var changes = new List<AuditFieldChange> { new("AssignedApproverUserId", t.AssignedApproverUserId.ToString(), newUserId.ToString()) };
                        t.AssignedApproverUserId = newUserId;
                        return Task.FromResult(changes);
                    }
                };
            }
            case ReassignmentItemTypes.PeriodicReview:
            {
                var t = await _db.PeriodicReviewTasks
                            .Include(x => x.AssignedReviewerUser)
                            .Include(x => x.DocumentRevision)
                            .Include(x => x.DocumentMaster).ThenInclude(m => m.Department)
                            .FirstOrDefaultAsync(x => x.Id == itemId && (x.Status == PeriodicReviewTaskStatus.Pending || x.Status == PeriodicReviewTaskStatus.InProgress))
                        ?? throw new KeyNotFoundException($"Periodic review {itemId} was not found or is no longer open.");
                if (t.AssignedReviewerUser is null)
                    throw new InvalidOperationException("This periodic review has no reviewer to replace.");
                EnsureHolderInactive(t.AssignedReviewerUser);
                return new Target
                {
                    ItemType = itemType, ItemId = itemId, MasterId = t.DocumentMasterId, RevisionId = t.DocumentRevisionId,
                    DepartmentId = t.DocumentMaster.DepartmentId, DepartmentName = t.DocumentMaster.Department.Name,
                    Code = t.DocumentMaster.CompanyDocumentCode, Title = t.DocumentMaster.Title, HolderUserId = t.AssignedReviewerUserId!.Value,
                    Role = "Periodic reviewer", Permission = PermissionConstants.DocumentsPeriodicReview,
                    Excluded = new HashSet<int> { t.DocumentRevision.CreatedByUserId },
                    Category = AuditActionCategory.Review, ActionCode = "PeriodicReviewReassigned", RecordType = nameof(PeriodicReviewTask),
                    Apply = (newUserId, _) =>
                    {
                        var changes = new List<AuditFieldChange> { new("AssignedReviewerUserId", t.AssignedReviewerUserId?.ToString(), newUserId.ToString()) };
                        t.AssignedReviewerUserId = newUserId;
                        return Task.FromResult(changes);
                    }
                };
            }
            default:
                throw new InvalidOperationException($"Unknown item type '{itemType}'. Expected one of: {string.Join(", ", ReassignmentItemTypes.All)}.");
        }
    }

    private static void EnsureHolderInactive(User? holder)
    {
        if (holder is { IsActive: true })
            throw new InvalidOperationException("This responsibility is held by an active user. Only responsibilities held by deactivated users are reassigned here.");
    }

    // Reactivates or creates the (document, user, role) assignment.
    private async Task GrantAssignmentAsync(int masterId, int userId, AssignmentRole role, DateTime now)
    {
        var existing = await _db.DocumentMasterAssignments
            .FirstOrDefaultAsync(x => x.DocumentMasterId == masterId && x.UserId == userId && x.AssignmentRole == role);
        if (existing is null)
        {
            _db.DocumentMasterAssignments.Add(new DocumentMasterAssignment
            {
                DocumentMasterId = masterId, UserId = userId, AssignmentRole = role,
                IsActive = true, AssignedAt = now, AssignedByUserId = _actingUserId
            });
        }
        else if (!existing.IsActive)
        {
            existing.IsActive = true;
            existing.AssignedAt = now;
            existing.AssignedByUserId = _actingUserId;
        }
    }

    private int _actingUserId;

    // ---- candidates ----------------------------------------------------------------------------

    private async Task<List<ReassignmentCandidateDto>> FindCandidatesAsync(Target target)
    {
        var excluded = target.Excluded.Append(target.HolderUserId).ToList();
        return await (
            from u in _db.Users.AsNoTracking()
            where u.IsActive
                  && !excluded.Contains(u.Id)
                  && _db.UserOrgMemberships.Any(m => m.UserId == u.Id && m.DepartmentId == target.DepartmentId)
                  && _db.RolePermissions.Any(rp => rp.RoleId == u.RoleId && rp.Permission!.Code == target.Permission)
            orderby u.FullName
            select new ReassignmentCandidateDto(u.Id, u.FullName, u.JobTitle, u.Role != null ? u.Role.Name : "")
        ).ToListAsync();
    }

    public async Task<IReadOnlyList<ReassignmentCandidateDto>> GetCandidatesAsync(string itemType, int itemId, int userId)
    {
        var scope = await GetScopeAsync(userId);
        var target = await ResolveAsync(itemType, itemId);
        EnsureCovers(scope, target.DepartmentId);
        return await FindCandidatesAsync(target);
    }

    // ---- the hand-over -------------------------------------------------------------------------

    public Task<ReassignmentResultDto> ReassignAsync(ReassignRequest request, int userId) =>
        UnitOfWork.RunAsync(_db, () => ReassignCoreAsync(request, userId));

    private async Task<ReassignmentResultDto> ReassignCoreAsync(ReassignRequest request, int userId)
    {
        var scope = await GetScopeAsync(userId);
        var reason = AdminActionReason.Require(request.Reason, "reassign a document responsibility");

        var target = await ResolveAsync(request.ItemType, request.ItemId);
        EnsureCovers(scope, target.DepartmentId);

        var newUser = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == request.NewUserId)
            ?? throw new InvalidOperationException($"User {request.NewUserId} was not found.");
        if (!newUser.IsActive)
            throw new InvalidOperationException($"User '{newUser.FullName}' is inactive and cannot take over a responsibility.");
        if (newUser.Id == target.HolderUserId)
            throw new InvalidOperationException("The new holder must be a different user.");
        if (!await _db.UserOrgMemberships.AnyAsync(m => m.UserId == newUser.Id && m.DepartmentId == target.DepartmentId))
            throw new InvalidOperationException($"User '{newUser.FullName}' is not a member of the department '{target.DepartmentName}' that owns this document.");
        if (!await _auth.HoldsPermissionAsync(newUser.Id, target.Permission))
            throw new InvalidOperationException($"User '{newUser.FullName}' cannot take over as {target.Role}: their role does not hold the {target.Permission} permission.");
        if (target.Excluded.Contains(newUser.Id))
            throw new UnauthorizedAccessException($"Segregation of Duties Violation: '{newUser.FullName}' took part in this revision as its author or technical reviewer and cannot take over as {target.Role}.");

        var now = _time.GetUtcNow().UtcDateTime;
        _actingUserId = userId;
        var previousUserId = target.HolderUserId;
        var changes = await target.Apply(newUser.Id, now);

        _db.CurrentUserId = userId;
        _db.CurrentReason = reason;
        await _db.SaveChangesAsync();

        await _audit.RecordUserEventAsync(
            actionCode: target.ActionCode,
            actionCategory: target.Category,
            recordType: target.RecordType,
            documentMasterId: target.MasterId,
            documentRevisionId: target.RevisionId,
            reason: reason,
            changes: changes,
            entityId: target.ItemId.ToString());

        await NotifyAsync(newUser.Id,
            NotificationType,
            $"You are now {target.Role} for {target.Code} – {target.Title} (handed over from a deactivated user). Reason: {reason}",
            "info");

        return new ReassignmentResultDto(target.ItemType, target.ItemId, target.MasterId, previousUserId, newUser.Id, newUser.FullName);
    }

    // The existing notification mechanism: a persisted NotificationLog (shown in the user's notifications) plus the in-process push.
    private async Task NotifyAsync(int recipientId, string type, string message, string severity)
    {
        _db.NotificationLogs.Add(new NotificationLog
        {
            UserId = recipientId, Type = type, Message = message, Severity = severity,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();
        if (_push is not null)
            await _push.NotifyAsync(recipientId, message);
    }

    // ---- safety net ----------------------------------------------------------------------------

    public async Task<int> NotifyOverdueAsync(DateTime? utcNowOverride = null, CancellationToken cancellationToken = default)
    {
        var now = utcNowOverride ?? _time.GetUtcNow().UtcDateTime;
        var days = await EscalationDaysAsync();
        var overdue = (await LoadAsync(null)).Where(r => IsOverdue(r.DeactivatedAt, now, days)).ToList();
        if (overdue.Count == 0) return 0;

        var created = 0;
        var recent = now - OverdueNotificationDedupe;
        foreach (var department in overdue.GroupBy(r => (r.DepartmentId, r.DepartmentName)))
        {
            var sectionHeads = await (
                from m in _db.UserOrgMemberships.AsNoTracking()
                where m.DepartmentId == department.Key.DepartmentId && m.User.IsActive
                      && _db.RolePermissions.Any(rp => rp.RoleId == m.User.RoleId && rp.Permission!.Code == PermissionConstants.DocumentsReassign)
                select m.UserId
            ).Distinct().ToListAsync(cancellationToken);

            var count = department.Count();
            var prefix = $"[{department.Key.DepartmentName}] ";
            var message = $"{prefix}{count} document responsibilit{(count == 1 ? "y" : "ies")} held by deactivated users {(count == 1 ? "has" : "have")} waited more than {days} days for reassignment. Open Document Control > Needs reassignment.";

            foreach (var recipient in sectionHeads)
            {
                var alreadyToldToday = await _db.NotificationLogs.AnyAsync(n =>
                    n.UserId == recipient && n.Type == OverdueNotificationType && n.CreatedAt >= recent && n.Message.StartsWith(prefix), cancellationToken);
                if (alreadyToldToday) continue;
                await NotifyAsync(recipient, OverdueNotificationType, message, "warning");
                created++;
            }
        }
        return created;
    }
}
