using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentAuthorizationService : IDocumentAuthorizationService
{
    private readonly MicroLimsDbContext _db;

    public DocumentAuthorizationService(MicroLimsDbContext db)
    {
        _db = db;
    }

    private async Task<(bool IsActive, RoleType? RoleType)> GetUserRoleAsync(int userId)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.IsActive)
            return (false, null);

        return (true, user.Role?.Type);
    }

    // Category access comes from the role's granted permissions, so a lab can
    // change who may perform a Document Control function from the Roles screen
    // instead of needing a code change (URS v1.1 §5: "Global role permissions
    // determine which categories of function a user may access").
    //
    // Per-document authority and the segregation-of-duties invariants are NOT
    // expressed this way - they stay as fixed rules below, because BR-013 states
    // the SoD restriction "cannot be overridden administratively".
    private async Task<bool> HasPermissionAsync(int userId, string permissionCode)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.IsActive)
            return false;

        return await _db.RolePermissions
            .Include(rp => rp.Permission)
            .AsNoTracking()
            .AnyAsync(rp => rp.RoleId == user.RoleId && rp.Permission!.Code == permissionCode);
    }

    private async Task<bool> IsOwnerOrAssignedAuthorAsync(int documentMasterId, int userId)
    {
        var isOwner = await _db.DocumentMasters
            .AsNoTracking()
            .AnyAsync(d => d.Id == documentMasterId && d.DocumentOwnerUserId == userId);

        if (isOwner) return true;

        var isAuthor = await _db.DocumentMasterAssignments
            .AsNoTracking()
            .AnyAsync(a => a.DocumentMasterId == documentMasterId &&
                           a.UserId == userId &&
                           a.AssignmentRole == AssignmentRole.Author &&
                           a.IsActive);

        return isAuthor;
    }

    // Registering a Document Master had no authorization check of any kind:
    // RegisterDocumentMasterAsync validated its input and the controller carried
    // only a bare [Authorize], so any authenticated user could create a master
    // and permanently consume a MicroLIMS Document ID (FS-1a-121 never reissues
    // one).
    //
    // Granted by permission rather than hardcoded role, because RoleType has no
    // Document Author: Analyst covers both Document Author (the FRS-1A matrix
    // grants registration) and General Reader (the same matrix denies it), so no
    // role check can satisfy both rows. Documents.Register is seeded to
    // Administrator, Document Controller and Analyst, and a lab that wants
    // registration restricted further revokes it on the Roles screen.
    public async Task<bool> CanRegisterDocumentMasterAsync(int userId)
    {
        return await HasPermissionAsync(userId, PermissionConstants.DocumentsRegister);
    }

    public async Task<bool> CanViewDocumentAsync(int documentMasterId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        var doc = await _db.DocumentMasters.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentMasterId);
        if (doc == null) return false;

        // Cancelled/Voided records require privileged view (Admin or Document Controller)
        if (doc.RecordStatus == DocumentRecordStatus.Void)
        {
            return role == RoleType.SystemAdministrator || role == RoleType.SectionHead;
        }

        return true;
    }

    public async Task<bool> CanEditDraftMetadataAsync(int documentMasterId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        if (role == RoleType.SystemAdministrator || role == RoleType.SectionHead)
            return true;

        return await IsOwnerOrAssignedAuthorAsync(documentMasterId, userId);
    }

    public async Task<bool> CanUploadOrReplaceDraftFileAsync(int revisionId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        var revision = await _db.DocumentRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId);
        if (revision == null || revision.RevisionStatus != DocumentRevisionStatus.Draft)
            return false;

        if (role == RoleType.SystemAdministrator || role == RoleType.SectionHead)
            return true;

        return await IsOwnerOrAssignedAuthorAsync(revision.DocumentMasterId, userId);
    }

    public async Task<bool> CanCancelDraftRevisionAsync(int revisionId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        var revision = await _db.DocumentRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId);
        if (revision == null || revision.RevisionStatus != DocumentRevisionStatus.Draft)
            return false;

        if (role == RoleType.SystemAdministrator || role == RoleType.SectionHead)
            return true;

        return await IsOwnerOrAssignedAuthorAsync(revision.DocumentMasterId, userId);
    }

    public async Task<bool> CanVoidDocumentMasterAsync(int documentMasterId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        // Quality rule FS-1a-111: strictly Document Controller (SectionHead role), not even SystemAdministrator
        return role == RoleType.SectionHead;
    }

    public async Task<bool> CanAccessSourceFileAsync(int fileId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        var file = await _db.RevisionFiles
            .Include(f => f.DocumentRevision)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null || file.DocumentRevision == null) return false;

        // Source access applies strictly to SourceFile
        if (file.FileRole != FileRole.SourceFile)
            return false;

        // 1. Privileged Governance Roles: SystemAdministrator or Document Controller (SectionHead)
        if (role == RoleType.SystemAdministrator || role == RoleType.SectionHead)
            return true;

        // 2. Author/Owner: Must be the designated Document Owner or assigned Author for THIS specific document
        var isAuthor = await IsOwnerOrAssignedAuthorAsync(file.DocumentRevision.DocumentMasterId, userId);
        if (isAuthor)
            return true;

        // 3. Technical Reviewer: Must be assigned as active reviewer on an open review task for THIS specific revision
        // Access is read-only and denied once the review assignment is no longer active (Completed, ReturnedForCorrection, Cancelled)
        var isActiveReviewer = await _db.DocumentReviewTasks
            .AsNoTracking()
            .AnyAsync(t => t.DocumentRevisionId == file.DocumentRevisionId &&
                           t.AssignedReviewerUserId == userId &&
                           (t.Status == ReviewTaskStatus.Pending || t.Status == ReviewTaskStatus.InProgress));

        if (isActiveReviewer)
            return true;

        // 4. Approver: Must be assigned to an active approval task for THIS specific revision
        // Access is read-only and denied once the approval task is no longer active (Approved, ReturnedForCorrection, Declined, Cancelled)
        var isActiveApprover = await _db.DocumentApprovalTasks
            .AsNoTracking()
            .AnyAsync(t => t.DocumentRevisionId == file.DocumentRevisionId &&
                           t.AssignedApproverUserId == userId &&
                           t.Status == DocumentApprovalTaskStatus.Pending);

        if (isActiveApprover)
            return true;

        return false;
    }

    public async Task<bool> CanAccessControlledPdfAsync(int fileId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        var file = await _db.RevisionFiles
            .Include(f => f.DocumentRevision)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId);

        if (file == null || file.DocumentRevision == null) return false;

        // If revision is effective: all active users can read
        if (file.DocumentRevision.RevisionStatus == DocumentRevisionStatus.Effective)
            return true;

        // Draft or other non-effective states: restricted to Admin, Doc Controller, or Owner/Author
        if (role == RoleType.SystemAdministrator || role == RoleType.SectionHead || role == RoleType.Reviewer)
            return true;

        return await IsOwnerOrAssignedAuthorAsync(file.DocumentRevision.DocumentMasterId, userId);
    }

    public async Task<bool> CanManageConfigurationAsync(int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        return isActive && role == RoleType.SystemAdministrator;
    }

    public async Task<bool> CanQueryGlobalAuditAsync(int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        // Admin, Document Controller, or QA Reviewer/Auditor
        return role == RoleType.SystemAdministrator || role == RoleType.SectionHead || role == RoleType.Reviewer;
    }

    public async Task<bool> CanViewRecordAuditAsync(int documentMasterId, int userId)
    {
        var (isActive, role) = await GetUserRoleAsync(userId);
        if (!isActive || role == null) return false;

        if (role == RoleType.SystemAdministrator || role == RoleType.SectionHead || role == RoleType.Reviewer)
            return true;

        return await IsOwnerOrAssignedAuthorAsync(documentMasterId, userId);
    }
}
