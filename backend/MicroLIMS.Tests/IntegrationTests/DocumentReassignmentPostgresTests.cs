using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The queue, candidate, hand-over, escalation and deactivation-impact queries translate to SQL and behave on
// PostgreSQL the way they do in the in-memory unit tests.
[Collection("PostgresDatabaseCollection")]
public class DocumentReassignmentPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public DocumentReassignmentPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task<Role> RoleAsync(MicroLimsDbContext db, string name, params string[] codes)
    {
        var role = new Role { Name = name, Type = RoleType.Analyst, IsSystemRole = false, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        foreach (var code in codes)
        {
            var id = await db.Permissions.Where(p => p.Code == code).Select(p => p.Id).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });
        }
        await db.SaveChangesAsync();
        return role;
    }

    [PostgresFact]
    public async Task Queue_Candidates_Reassignment_Escalation_AndImpact_WorkOnPostgreSql()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();
        var tag = Guid.NewGuid().ToString("N")[..6];

        var actorRole = await RoleAsync(db, $"R-actor-{tag}", PermissionConstants.DocumentsConfigManage);
        var headRole = await RoleAsync(db, $"R-head-{tag}", PermissionConstants.DocumentsReassign, PermissionConstants.DocumentsDraftEdit);
        var workerRole = await RoleAsync(db, $"R-worker-{tag}", PermissionConstants.DocumentsDraftEdit, PermissionConstants.DocumentsReview,
            PermissionConstants.DocumentsApprove, PermissionConstants.DocumentsPeriodicReview);

        User NewUser(string n, Role r, bool active = true, DateTime? deactivatedAt = null) =>
            new() { Username = $"{n}-{tag}", FullName = $"{n} {tag}", PasswordHash = "x", RoleId = r.Id, IsActive = active, DeactivatedAt = deactivatedAt };
        var actor = NewUser("actor", actorRole);
        var head = NewUser("head", headRole);
        var author = NewUser("author", workerRole);
        var worker = NewUser("worker", workerRole);
        var approver = NewUser("approver", workerRole);
        var gone = NewUser("gone", workerRole, active: false, deactivatedAt: DateTime.UtcNow.AddDays(-30));
        db.Users.AddRange(actor, head, author, worker, approver, gone);
        var dept = new DocumentDepartment { Code = $"T{tag}", Name = $"Dept {tag}", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var section = new DocumentSection { DepartmentId = dept.Id, Name = $"Sec {tag}", Code = $"S{tag}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = head.Id, DepartmentId = dept.Id },
            new UserOrgMembership { UserId = author.Id, DepartmentId = dept.Id },
            new UserOrgMembership { UserId = worker.Id, DepartmentId = dept.Id, SectionId = section.Id },
            new UserOrgMembership { UserId = approver.Id, DepartmentId = dept.Id });

        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"PG-{tag}", CompanyDocumentCode = $"PG-{tag}", Title = "PG SOP", DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = dept.Id, SectionId = section.Id, DocumentOwnerUserId = gone.Id, CreatedByUserId = author.Id, Confidentiality = DocumentConfidentiality.Internal
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();
        db.DocumentMasterSections.Add(new DocumentMasterSection { DocumentMasterId = master.Id, SectionId = section.Id });
        var revision = new DocumentRevision { DocumentMasterId = master.Id, RevisionNumber = "01", RevisionSequence = 1, RevisionStatus = DocumentRevisionStatus.InReview, CreatedByUserId = author.Id };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();
        var assignment = new DocumentMasterAssignment { DocumentMasterId = master.Id, UserId = gone.Id, AssignmentRole = AssignmentRole.Author, AssignedByUserId = actor.Id };
        var review = new DocumentReviewTask { DocumentRevisionId = revision.Id, AssignedReviewerUserId = gone.Id, AssignedByUserId = actor.Id, Status = ReviewTaskStatus.Pending };
        var approval = new DocumentApprovalTask { DocumentRevisionId = revision.Id, AssignedApproverUserId = gone.Id, AssignedByUserId = actor.Id, Status = DocumentApprovalTaskStatus.Pending };
        var periodic = new PeriodicReviewTask { DocumentMasterId = master.Id, DocumentRevisionId = revision.Id, ReviewCycleMonths = 24, ScheduledDueDate = DateTime.UtcNow, AssignedReviewerUserId = gone.Id, Status = PeriodicReviewTaskStatus.InProgress };
        db.DocumentMasterAssignments.Add(assignment);
        db.DocumentReviewTasks.Add(review);
        db.DocumentApprovalTasks.Add(approval);
        db.PeriodicReviewTasks.Add(periodic);
        await db.SaveChangesAsync();

        var seq = _fixture.CreateSequenceHelper(db);
        var audit = new AuditEventService(db, seq);
        var auth = new DocumentAuthorizationService(db);
        var svc = new DocumentReassignmentService(db, audit, auth, new SpyNotificationService());

        // Queue: all five kinds, scoped per department.
        var queue = await svc.GetQueueAsync(dept.Id, head.Id);
        Assert.Equal(5, queue.Total);
        Assert.Equal(5, queue.Overdue);
        Assert.Equal(new[] { "ApprovalTask", "Assignment", "Custodian", "PeriodicReview", "ReviewTask" }, queue.Items.Select(i => i.ItemType).OrderBy(t => t).ToArray());
        Assert.All(queue.Items, i => Assert.Equal(($"PG-{tag}", "01"), (i.DocumentCode, i.RevisionNumber)));
        Assert.Equal(5, (await svc.GetCountAsync(actor.Id)).Total - (await svc.GetQueueAsync(null, actor.Id)).Items.Count(i => i.DepartmentId != dept.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetQueueAsync(dept.Id, worker.Id));

        // Candidates: active members holding the code; SoD removes the revision's author from review roles.
        var custodianCandidates = await svc.GetCandidatesAsync(ReassignmentItemTypes.Custodian, master.Id, head.Id);
        Assert.Equal(new[] { author.Id, head.Id, worker.Id, approver.Id }.OrderBy(i => i), custodianCandidates.Select(c => c.UserId).OrderBy(i => i));
        var reviewerCandidates = await svc.GetCandidatesAsync(ReassignmentItemTypes.ReviewTask, review.Id, head.Id);
        Assert.Equal(new[] { worker.Id, approver.Id }.OrderBy(i => i), reviewerCandidates.Select(c => c.UserId).OrderBy(i => i));

        // Hand-overs.
        // (the reviewer cannot also approve: segregation of duties sends the approval to someone else)
        foreach (var (type, id, to) in new[]
                 {
                     (ReassignmentItemTypes.Custodian, master.Id, worker.Id), (ReassignmentItemTypes.Assignment, assignment.Id, worker.Id),
                     (ReassignmentItemTypes.ReviewTask, review.Id, worker.Id), (ReassignmentItemTypes.ApprovalTask, approval.Id, approver.Id),
                     (ReassignmentItemTypes.PeriodicReview, periodic.Id, worker.Id)
                 })
            await svc.ReassignAsync(new ReassignRequest(type, id, to, "PostgreSQL hand-over"), head.Id);
        db.ChangeTracker.Clear();

        Assert.Equal(0, (await svc.GetQueueAsync(dept.Id, head.Id)).Total);
        Assert.Equal(worker.Id, (await db.DocumentMasters.AsNoTracking().SingleAsync(m => m.Id == master.Id)).DocumentOwnerUserId);
        Assert.Equal(5, await db.AuditLogs.CountAsync(a => a.EntityName != "NotificationLog" && a.Reason == "PostgreSQL hand-over" && a.ActionCode != null && a.ActionCode.EndsWith("Reassigned")));
        Assert.Equal(4, await db.NotificationLogs.CountAsync(n => n.UserId == worker.Id && n.Type == DocumentReassignmentService.NotificationType));
        Assert.Equal(1, await db.NotificationLogs.CountAsync(n => n.UserId == approver.Id && n.Type == DocumentReassignmentService.NotificationType));

        // Escalation: a fresh orphaned item after the clock has run, reported to the department's Section Head.
        gone.IsActive = false;
        db.Users.Update(gone);
        db.DocumentReviewTasks.Add(new DocumentReviewTask { DocumentRevisionId = revision.Id, AssignedReviewerUserId = gone.Id, AssignedByUserId = actor.Id, Status = ReviewTaskStatus.Pending, ReviewCycleNumber = 2 });
        await db.SaveChangesAsync();
        Assert.Equal(1, await svc.NotifyOverdueAsync());
        Assert.Equal(1, await db.NotificationLogs.CountAsync(n => n.UserId == head.Id && n.Type == DocumentReassignmentService.OverdueNotificationType));

        // Deactivation impact (documents, active members, open work) as SQL.
        var config = new DocumentConfigurationService(db, audit, auth);
        var impact = await config.GetDepartmentImpactAsync(dept.Id, actor.Id);
        Assert.Equal(1, impact.ActiveDocuments);
        Assert.Equal(4, impact.ActiveMemberships);
        Assert.Equal(2, impact.OpenWork); // the in-review revision and the in-progress periodic review
        var sectionImpact = await config.GetSectionImpactAsync(section.Id, actor.Id);
        Assert.Equal((1, 1), (sectionImpact.ActiveDocuments, sectionImpact.ActiveMemberships));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            config.UpdateSectionAsync(section.Id, new UpdateDocumentSectionRequest(section.Name, false, "closing"), actor.Id));
    }
}
