using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Application.Services.MyWork.Sources;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Documents and equipment sources of My Work against a real PostgreSQL database.
// Every act goes through MyWorkService on its own DbContext; arrange and assert use others.
[Collection("PostgresDatabaseCollection")]
public class MyWorkOtherSourcesPostgresTests
{
    // 12:00 UTC on 14 Nov 2026 is 14:00 in Cairo (UTC+2); the laboratory day ends at 22:00 UTC.
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public MyWorkOtherSourcesPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class Settings : ISystemSettingsReader
    {
        private readonly int _lookAheadDays;
        public Settings(int lookAheadDays = 7) => _lookAheadDays = lookAheadDays;

        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == SystemSettingKeys.MyWorkLookAheadDays ? _lookAheadDays : 0);
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }

    private sealed class RecordingErrors : IErrorCaptureService
    {
        public List<ErrorCaptureRequest> Captured { get; } = new();
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default)
        {
            Captured.Add(request);
            return Task.CompletedTask;
        }
    }

    private static MyWorkService BuildService(MicroLimsDbContext db, DateTime? nowUtc = null, string zoneId = "Africa/Cairo",
        int lookAheadDays = 7, RecordingErrors? errors = null)
    {
        var clock = new LabClock(new FixedTimeProvider(new DateTimeOffset(nowUtc ?? Now)), LabClock.ResolveTimeZone(zoneId));
        return new MyWorkService(
            db,
            new UserSectionScopeService(db),
            clock,
            new Settings(lookAheadDays),
            errors ?? new RecordingErrors(),
            new IMyWorkSource[] { new DocumentsSource(db), new EquipmentSource(db, clock) });
    }

    private async Task<MyWorkResponse> GetAsync(int userId, DateTime? nowUtc = null, string zoneId = "Africa/Cairo", int lookAheadDays = 7)
    {
        await using var actDb = _fixture.CreateDbContext();
        var errors = new RecordingErrors();
        var service = BuildService(actDb, nowUtc, zoneId, lookAheadDays, errors);
        var response = await service.GetAsync(userId, null);
        Assert.Empty(response.UnavailableSources);
        Assert.Empty(errors.Captured);
        return response;
    }

    private static MyWorkGroup? Group(MyWorkResponse r, string key) => r.Groups.SingleOrDefault(g => g.Key == key);

    private static string Uid() => Guid.NewGuid().ToString("N")[..8];

    private async Task<User> NewUserAsync(MicroLimsDbContext db, string label, bool withEquipmentWorkRecord = false)
    {
        var uid = Uid();
        Role role;
        if (withEquipmentWorkRecord)
        {
            role = new Role { Type = RoleType.Analyst, Name = $"EqWorker {uid}", IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            var perm = await db.Permissions.SingleAsync(p => p.Code == PermissionConstants.EquipmentWorkRecord);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
        }
        else
        {
            role = new Role { Type = RoleType.Analyst, Name = $"PlainDocUser {uid}", IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            Username = $"mw-{label}-{uid}",
            Email = $"mw-{label}-{uid}@example.test",
            PasswordHash = "hash",
            FullName = $"MW {label} {uid}",
            RoleId = role.Id,
            IsActive = true,
            MustChangePassword = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<(DocumentMaster Master, DocumentRevision Revision)> NewDocumentAsync(MicroLimsDbContext db, int ownerUserId)
    {
        var uid = Uid();
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"DOC-MW-{uid}",
            CompanyDocumentCode = $"SOP-MW-{uid}",
            Title = $"MyWork Doc {uid}",
            DocumentTypeId = _fixture.SeededDocTypeId,
            DepartmentId = _fixture.SeededDepartmentId,
            SectionId = _fixture.SeededSectionId,
            DocumentOwnerUserId = ownerUserId,
            CreatedByUserId = ownerUserId,
            RecordStatus = DocumentRecordStatus.Active
        };
        db.DocumentMasters.Add(master);
        await db.SaveChangesAsync();

        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id,
            RevisionNumber = "01",
            RevisionSequence = 1,
            RevisionStatus = DocumentRevisionStatus.Draft,
            CreatedByUserId = ownerUserId
        };
        db.DocumentRevisions.Add(revision);
        await db.SaveChangesAsync();
        return (master, revision);
    }

    private async Task<EquipmentInventory> NewEquipmentAsync(MicroLimsDbContext db)
    {
        var uid = Uid();
        var eq = new EquipmentInventory
        {
            Code = $"EQ-MW-{uid}",
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            Location = "Lab 1",
            Status = EquipmentOperationalStatus.InService,
            CalibrationIntervalValue = 12,
            CalibrationIntervalUnit = IntervalUnit.Months,
            PmIntervalValue = 1,
            PmIntervalUnit = IntervalUnit.Months,
            SectionId = _fixture.SeededSectionId
        };
        db.EquipmentInventories.Add(eq);
        await db.SaveChangesAsync();
        return eq;
    }

    [PostgresFact]
    public async Task ReviewTaskAssignedToMe_Appears_NotForOthers()
    {
        int aliceId, bobId, myTaskId, otherTaskId, docId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var alice = await NewUserAsync(arrangeDb, "alice");
            var bob = await NewUserAsync(arrangeDb, "bob");
            var (master, rev) = await NewDocumentAsync(arrangeDb, _fixture.SeededUserId);
            var myTask = new DocumentReviewTask
            {
                DocumentRevisionId = rev.Id,
                AssignedReviewerUserId = alice.Id,
                AssignedByUserId = _fixture.SeededUserId,
                AssignedAt = Now.AddHours(-2),
                DueDate = Now.AddDays(2),
                Status = ReviewTaskStatus.Pending
            };
            var otherTask = new DocumentReviewTask
            {
                DocumentRevisionId = rev.Id,
                AssignedReviewerUserId = bob.Id,
                AssignedByUserId = _fixture.SeededUserId,
                AssignedAt = Now.AddHours(-2),
                DueDate = Now.AddDays(2),
                Status = ReviewTaskStatus.InProgress
            };
            arrangeDb.DocumentReviewTasks.AddRange(myTask, otherTask);
            await arrangeDb.SaveChangesAsync();
            aliceId = alice.Id;
            bobId = bob.Id;
            myTaskId = myTask.Id;
            otherTaskId = otherTask.Id;
            docId = master.Id;
        }

        var aliceResp = await GetAsync(aliceId);
        var bobResp = await GetAsync(bobId);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var saved = await assertDb.DocumentReviewTasks.AsNoTracking().SingleAsync(t => t.Id == myTaskId);
            Assert.Equal(aliceId, saved.AssignedReviewerUserId);
        }

        var aliceDocs = Group(aliceResp, "documents");
        Assert.NotNull(aliceDocs);
        var item = Assert.Single(aliceDocs!.Items, i => i.Key == $"document-review:{myTaskId}");
        Assert.Equal("document-review", item.Target.Type);
        Assert.Equal(docId, item.Target.DocumentId);
        Assert.Equal(myTaskId, item.Target.TaskId);
        Assert.DoesNotContain(aliceDocs.Items, i => i.Key == $"document-review:{otherTaskId}");

        var bobDocs = Group(bobResp, "documents");
        Assert.NotNull(bobDocs);
        Assert.Contains(bobDocs!.Items, i => i.Key == $"document-review:{otherTaskId}");
        Assert.DoesNotContain(bobDocs.Items, i => i.Key == $"document-review:{myTaskId}");
    }

    [PostgresFact]
    public async Task ApprovalAssignedToAnotherApprover_Absent()
    {
        int aliceId, bobId, bobTaskId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var alice = await NewUserAsync(arrangeDb, "alice");
            var bob = await NewUserAsync(arrangeDb, "bob");
            var (_, rev) = await NewDocumentAsync(arrangeDb, _fixture.SeededUserId);
            var bobTask = new DocumentApprovalTask
            {
                DocumentRevisionId = rev.Id,
                AssignedApproverUserId = bob.Id,
                AssignedByUserId = _fixture.SeededUserId,
                AssignedAt = Now.AddHours(-1),
                DueDate = Now.AddDays(1),
                Status = DocumentApprovalTaskStatus.Pending
            };
            arrangeDb.DocumentApprovalTasks.Add(bobTask);
            await arrangeDb.SaveChangesAsync();
            aliceId = alice.Id;
            bobId = bob.Id;
            bobTaskId = bobTask.Id;
        }

        var aliceResp = await GetAsync(aliceId);
        var bobResp = await GetAsync(bobId);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var saved = await assertDb.DocumentApprovalTasks.AsNoTracking().SingleAsync(t => t.Id == bobTaskId);
            Assert.Equal(bobId, saved.AssignedApproverUserId);
        }

        var aliceDocs = Group(aliceResp, "documents");
        Assert.True(aliceDocs is null || !aliceDocs.Items.Any(i => i.Key == $"document-approval:{bobTaskId}"));

        var bobDocs = Group(bobResp, "documents");
        Assert.NotNull(bobDocs);
        var item = Assert.Single(bobDocs!.Items, i => i.Key == $"document-approval:{bobTaskId}");
        Assert.Equal("document-approval", item.Target.Type);
        Assert.Equal(bobTaskId, item.Target.TaskId);
    }

    [PostgresFact]
    public async Task TrainingAssignment_DuePast_IsOverdue_Completed_Absent()
    {
        int userId, overdueId, completedId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var user = await NewUserAsync(arrangeDb, "trainee");
            var (master, rev) = await NewDocumentAsync(arrangeDb, _fixture.SeededUserId);
            var (master2, rev2) = await NewDocumentAsync(arrangeDb, _fixture.SeededUserId);
            var overdue = new DocumentTrainingAssignment
            {
                DocumentMasterId = master.Id,
                DocumentRevisionId = rev.Id,
                AssignedUserId = user.Id,
                AssignmentType = AssignmentType.Reading,
                Status = TrainingAssignmentStatus.Assigned,
                AssignedDateUtc = Now.AddDays(-10),
                DueDateUtc = Now.AddHours(-2),
                CreatedByUserId = _fixture.SeededUserId
            };
            var completed = new DocumentTrainingAssignment
            {
                DocumentMasterId = master2.Id,
                DocumentRevisionId = rev2.Id,
                AssignedUserId = user.Id,
                AssignmentType = AssignmentType.Reading,
                Status = TrainingAssignmentStatus.Acknowledged,
                AssignedDateUtc = Now.AddDays(-10),
                DueDateUtc = Now.AddHours(-2),
                AcknowledgedAtUtc = Now.AddHours(-3),
                CompletedAtUtc = Now.AddHours(-3),
                CreatedByUserId = _fixture.SeededUserId
            };
            arrangeDb.DocumentTrainingAssignments.AddRange(overdue, completed);
            await arrangeDb.SaveChangesAsync();
            userId = user.Id;
            overdueId = overdue.Id;
            completedId = completed.Id;
        }

        var resp = await GetAsync(userId);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var savedOverdue = await assertDb.DocumentTrainingAssignments.AsNoTracking().SingleAsync(a => a.Id == overdueId);
            Assert.Equal(TrainingAssignmentStatus.Assigned, savedOverdue.Status);
        }

        var docs = Group(resp, "documents");
        Assert.NotNull(docs);
        var item = Assert.Single(docs!.Items, i => i.Key == $"training-assignment:{overdueId}");
        Assert.Equal(MyWorkUrgencyLevel.Overdue, item.Urgency);
        Assert.Equal("training-assignment", item.Target.Type);
        Assert.DoesNotContain(docs.Items, i => i.Key == $"training-assignment:{completedId}");
    }

    [PostgresFact]
    public async Task DocumentWithoutDueDate_UrgencyNone()
    {
        int userId, reviewId, approvalId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var user = await NewUserAsync(arrangeDb, "nodue");
            var (_, rev) = await NewDocumentAsync(arrangeDb, _fixture.SeededUserId);
            var review = new DocumentReviewTask
            {
                DocumentRevisionId = rev.Id,
                AssignedReviewerUserId = user.Id,
                AssignedByUserId = _fixture.SeededUserId,
                AssignedAt = Now.AddHours(-3),
                DueDate = null,
                Status = ReviewTaskStatus.Pending
            };
            var approval = new DocumentApprovalTask
            {
                DocumentRevisionId = rev.Id,
                AssignedApproverUserId = user.Id,
                AssignedByUserId = _fixture.SeededUserId,
                AssignedAt = Now.AddHours(-2),
                DueDate = null,
                Status = DocumentApprovalTaskStatus.Pending
            };
            arrangeDb.DocumentReviewTasks.Add(review);
            arrangeDb.DocumentApprovalTasks.Add(approval);
            await arrangeDb.SaveChangesAsync();
            userId = user.Id;
            reviewId = review.Id;
            approvalId = approval.Id;
        }

        var resp = await GetAsync(userId);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var savedReview = await assertDb.DocumentReviewTasks.AsNoTracking().SingleAsync(t => t.Id == reviewId);
            Assert.Null(savedReview.DueDate);
        }

        var docs = Group(resp, "documents");
        Assert.NotNull(docs);
        var revItem = Assert.Single(docs!.Items, i => i.Key == $"document-review:{reviewId}");
        var appItem = Assert.Single(docs.Items, i => i.Key == $"document-approval:{approvalId}");
        Assert.Equal(MyWorkUrgencyLevel.None, revItem.Urgency);
        Assert.Equal(MyWorkUrgencyLevel.None, appItem.Urgency);
    }

    [PostgresFact]
    public async Task EquipmentTask_DueIn7Days_Appears_In8_Absent_Completed_Absent_Retired_Absent()
    {
        int userId, eqId, in7Id, in8Id, completedId, retiredId;
        string sectionName;
        var labToday = new DateOnly(2026, 11, 14);
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var user = await NewUserAsync(arrangeDb, "eqworker", withEquipmentWorkRecord: true);
            var eq = await NewEquipmentAsync(arrangeDb);
            sectionName = await arrangeDb.DocumentSections.AsNoTracking()
                .Where(s => s.Id == eq.SectionId!.Value)
                .Select(s => s.Name)
                .SingleAsync();

            var revision = new EquipmentPlanRevision
            {
                Year = labToday.Year,
                RevisionNumber = 1,
                Status = PlanRevisionStatus.Approved,
                EffectiveFrom = labToday.AddDays(-30),
                GeneratedByUserId = _fixture.SeededUserId,
                GeneratedAt = Now.AddDays(-30)
            };
            arrangeDb.EquipmentPlanRevisions.Add(revision);
            await arrangeDb.SaveChangesAsync();

            var in7 = new EquipmentTask
            {
                EquipmentInventoryId = eq.Id,
                TaskType = EquipmentTaskType.Calibration,
                Year = labToday.Year,
                Sequence = 1,
                OriginalDueDate = labToday.AddDays(7),
                DueDate = labToday.AddDays(7),
                IntervalSnapshot = "12 months"
            };
            var in8 = new EquipmentTask
            {
                EquipmentInventoryId = eq.Id,
                TaskType = EquipmentTaskType.Calibration,
                Year = labToday.Year,
                Sequence = 2,
                OriginalDueDate = labToday.AddDays(8),
                DueDate = labToday.AddDays(8),
                IntervalSnapshot = "12 months"
            };
            var completed = new EquipmentTask
            {
                EquipmentInventoryId = eq.Id,
                TaskType = EquipmentTaskType.PreventiveMaintenance,
                Year = labToday.Year,
                Sequence = 3,
                OriginalDueDate = labToday.AddDays(1),
                DueDate = labToday.AddDays(1),
                CompletedOn = labToday,
                IntervalSnapshot = "1 month"
            };
            var retired = new EquipmentTask
            {
                EquipmentInventoryId = eq.Id,
                TaskType = EquipmentTaskType.PreventiveMaintenance,
                Year = labToday.Year,
                Sequence = 4,
                OriginalDueDate = labToday.AddDays(2),
                DueDate = labToday.AddDays(2),
                RetiredByRevisionId = revision.Id,
                IntervalSnapshot = "1 month"
            };
            arrangeDb.EquipmentTasks.AddRange(in7, in8, completed, retired);
            await arrangeDb.SaveChangesAsync();
            userId = user.Id;
            eqId = eq.Id;
            in7Id = in7.Id;
            in8Id = in8.Id;
            completedId = completed.Id;
            retiredId = retired.Id;
        }

        var resp = await GetAsync(userId);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var savedIn7 = await assertDb.EquipmentTasks.AsNoTracking().SingleAsync(t => t.Id == in7Id);
            Assert.Null(savedIn7.CompletedOn);
            Assert.Null(savedIn7.RetiredByRevisionId);
        }

        var eqGroup = Group(resp, "equipment");
        Assert.NotNull(eqGroup);
        var item = Assert.Single(eqGroup!.Items, i => i.Key == $"equipment-task:{in7Id}");
        Assert.Equal("equipment-task", item.Target.Type);
        Assert.Equal(in7Id, item.Target.TaskId);
        Assert.Equal(eqId, item.Target.EquipmentId);
        Assert.Contains(sectionName, item.Context);
        Assert.Equal(MyWorkUrgencyLevel.Later, item.Urgency);
        Assert.DoesNotContain(eqGroup.Items, i => i.Key == $"equipment-task:{in8Id}");
        Assert.DoesNotContain(eqGroup.Items, i => i.Key == $"equipment-task:{completedId}");
        Assert.DoesNotContain(eqGroup.Items, i => i.Key == $"equipment-task:{retiredId}");
    }

    [PostgresFact]
    public async Task EquipmentTask_DueYesterdayLabDate_IsOverdue()
    {
        // 22:30 UTC on 14 Nov 2026 is 00:30 on 15 Nov 2026 in Africa/Cairo (UTC+2).
        // So DueDate = 14 Nov 2026 is yesterday in lab time (Overdue), even though it is today in UTC.
        var lateUtc = new DateTime(2026, 11, 14, 22, 30, 0, DateTimeKind.Utc);
        var dueYesterdayLab = new DateOnly(2026, 11, 14);
        int userId, taskId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var user = await NewUserAsync(arrangeDb, "eqtz", withEquipmentWorkRecord: true);
            var eq = await NewEquipmentAsync(arrangeDb);
            var task = new EquipmentTask
            {
                EquipmentInventoryId = eq.Id,
                TaskType = EquipmentTaskType.Calibration,
                Year = dueYesterdayLab.Year,
                Sequence = 1,
                OriginalDueDate = dueYesterdayLab,
                DueDate = dueYesterdayLab,
                IntervalSnapshot = "12 months"
            };
            arrangeDb.EquipmentTasks.Add(task);
            await arrangeDb.SaveChangesAsync();
            userId = user.Id;
            taskId = task.Id;
        }

        var resp = await GetAsync(userId, nowUtc: lateUtc, zoneId: "Africa/Cairo");

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var saved = await assertDb.EquipmentTasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
            Assert.Equal(dueYesterdayLab, saved.DueDate);
        }

        var eqGroup = Group(resp, "equipment");
        Assert.NotNull(eqGroup);
        var item = Assert.Single(eqGroup!.Items, i => i.Key == $"equipment-task:{taskId}");
        Assert.Equal(MyWorkUrgencyLevel.Overdue, item.Urgency);
    }

    [PostgresFact]
    public async Task WithoutEquipmentWorkRecord_SourceDoesNotApply()
    {
        int userId, taskId;
        var labToday = new DateOnly(2026, 11, 14);
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var user = await NewUserAsync(arrangeDb, "noeqperm", withEquipmentWorkRecord: false);
            var eq = await NewEquipmentAsync(arrangeDb);
            var task = new EquipmentTask
            {
                EquipmentInventoryId = eq.Id,
                TaskType = EquipmentTaskType.Calibration,
                Year = labToday.Year,
                Sequence = 1,
                OriginalDueDate = labToday,
                DueDate = labToday,
                IntervalSnapshot = "12 months"
            };
            arrangeDb.EquipmentTasks.Add(task);
            await arrangeDb.SaveChangesAsync();
            userId = user.Id;
            taskId = task.Id;
        }

        var resp = await GetAsync(userId);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var saved = await assertDb.EquipmentTasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
            Assert.NotNull(saved);
        }

        Assert.Null(Group(resp, "equipment"));
    }
}
