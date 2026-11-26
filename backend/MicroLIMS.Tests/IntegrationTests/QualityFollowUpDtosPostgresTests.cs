using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public sealed class QualityFollowUpDtosPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "QualityFollowUp-test-password";
    private static readonly DateTime NowUtc = new(2086, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider Clock = new FixedClock();

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(NowUtc, TimeSpan.Zero);
    }

    private sealed class MemoryStorage : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> _files = new();
        public Task<string> SaveAsync(string key, byte[] content)
        {
            _files[key] = content;
            return Task.FromResult(key);
        }
        public Task<byte[]> ReadAsync(string key) => Task.FromResult(_files[key]);
    }

    private sealed class DummyPdf : IPdfGenerator
    {
        public Task<byte[]> GenerateReportAsync(ReportDocument doc) => Task.FromResult(Array.Empty<byte>());
    }

    private MicroLimsDbContext Context(int? actor = null)
    {
        var db = fixture.CreateDbContext(Clock);
        db.CurrentUserId = actor;
        return db;
    }

    private static QualityRecordCore Core(MicroLimsDbContext db) => new(
        db,
        new PermissionService(db),
        new UserSectionScopeService(db),
        new ReferenceNumberGenerator(db, Clock),
        new ElectronicSignatureService(db, Clock),
        Clock);

    private static QualityCommentService Comments(MicroLimsDbContext db) => new(db, Core(db), Clock);

    private QualityAttachmentService Attachments(MicroLimsDbContext db, IFileStorageService storage) =>
        new(db, Core(db), storage, new AuditEventService(db, fixture.CreateSequenceHelper(db), Clock), Clock);

    private static QualityConfigService Config(MicroLimsDbContext db) => new(
        db,
        Core(db),
        new PermissionService(db),
        new UserSectionScopeService(db),
        Clock,
        new ElectronicSignatureService(db, Clock));

    private static QualityLinkService Links(MicroLimsDbContext db) => new(
        db,
        Core(db),
        new PermissionService(db),
        new UserSectionScopeService(db),
        Clock);

    private static QualityQueryService Queries(MicroLimsDbContext db) =>
        new(db, Core(db), Links(db), new LabClock(Clock, TimeZoneInfo.Utc));

    private async Task<int> CreateUserAsync(MicroLimsDbContext db, string fullName, RoleType roleType,
        int departmentId, int sectionId, params string[] permissions)
    {
        var key = Guid.NewGuid().ToString("N");
        var role = new Role { Name = key, Type = roleType, IsActive = true };
        db.Roles.Add(role);
        var user = new User
        {
            FullName = fullName,
            Username = key,
            Role = role,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var permIds = await db.Permissions.AsNoTracking()
            .Where(p => permissions.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();
        db.RolePermissions.AddRange(permIds.Select(pid => new RolePermission { RoleId = role.Id, PermissionId = pid }));
        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = departmentId,
            SectionId = sectionId
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(int SectionId, int AuthorId, int ApproverId, int RecordId, uint Version)> SeedDeviationAsync()
    {
        await using var arrange = Context();
        var sectionId = fixture.SeededSectionId;
        var deptId = fixture.SeededDepartmentId;

        var authorId = await CreateUserAsync(arrange, "Nadia Author", RoleType.Analyst, deptId, sectionId,
            PermissionConstants.QualityView, PermissionConstants.QualityRaise, PermissionConstants.QualityInvestigate);
        var approverId = await CreateUserAsync(arrange, "Omar Approver", RoleType.SectionHead, deptId, sectionId,
            PermissionConstants.QualityView, PermissionConstants.QualityRaise,
            PermissionConstants.QualityInvestigate, PermissionConstants.QualityApprove);

        arrange.CurrentUserId = authorId;
        var core = Core(arrange);
        var record = await core.CreateAsync(QualityRecordType.Deviation,
            new("Follow-up DTO test", "Testing user names and counts.", sectionId, null), authorId);
        record.OwnerUserId = authorId;
        record.DueDate = DateOnly.FromDateTime(NowUtc.Date).AddDays(5);
        arrange.DeviationDetails.Add(new DeviationDetail
        {
            RecordId = record.Id,
            OccurredAtUtc = NowUtc.AddHours(-1),
            ImmediateAction = "Isolated"
        });
        await arrange.SaveChangesAsync();
        return (sectionId, authorId, approverId, record.Id, record.Version);
    }

    [PostgresFact]
    public async Task Comment_Dto_Projects_Real_AuthorName_On_Add_And_List()
    {
        var (_, authorId, approverId, recordId, version) = await SeedDeviationAsync();

        QualityCommentDto added;
        await using (var act = Context(authorId))
        {
            added = await Comments(act).AddAsync(recordId, version, "Checked incubator.", authorId);
        }
        Assert.Equal("Nadia Author", added.AuthorName);

        await using var assertDb = Context(approverId);
        var listed = await Comments(assertDb).ListAsync(recordId, approverId, 0, 20);
        var item = Assert.Single(listed);
        Assert.Equal("Nadia Author", item.AuthorName);
    }

    [PostgresFact]
    public async Task Extension_Dto_Projects_RequestedByName_And_DecidedByName_Null_Until_Decided()
    {
        var (_, requesterId, deciderId, recordId, version) = await SeedDeviationAsync();
        var requestedDate = DateOnly.FromDateTime(NowUtc.Date).AddDays(12);

        QualityExtensionDto requested;
        await using (var act = Context(requesterId))
        {
            requested = await Config(act).RequestAsync(recordId, null, requestedDate, "Awaiting parts", version, requesterId);
        }
        Assert.Equal("Nadia Author", requested.RequestedByName);
        Assert.Null(requested.DecidedByName);

        await using (var assertBefore = Context(deciderId))
        {
            var beforeList = await Config(assertBefore).ListExtensionsAsync(recordId, 1, 20, deciderId);
            var item = Assert.Single(beforeList);
            Assert.Equal("Nadia Author", item.RequestedByName);
            Assert.Null(item.DecidedByName);
        }

        QualityExtensionDto decided;
        await using (var actDecide = Context(deciderId))
        {
            decided = await Config(actDecide).DecideAsync(requested.Id, true, "Approved", Password, requested.Version, deciderId);
        }
        Assert.Equal("Nadia Author", decided.RequestedByName);
        Assert.Equal("Omar Approver", decided.DecidedByName);

        await using var assertAfter = Context(requesterId);
        var afterList = await Config(assertAfter).ListExtensionsAsync(recordId, 1, 20, requesterId);
        var persisted = Assert.Single(afterList);
        Assert.Equal("Nadia Author", persisted.RequestedByName);
        Assert.Equal("Omar Approver", persisted.DecidedByName);
    }

    [PostgresFact]
    public async Task Attachment_Dto_Projects_UploadedByName_And_VoidedByName_Everywhere()
    {
        var (_, uploaderId, voiderId, recordId, version) = await SeedDeviationAsync();
        var storage = new MemoryStorage();

        QualityAttachmentDto uploaded;
        await using (var actUpload = Context(uploaderId))
        {
            uploaded = await Attachments(actUpload, storage).UploadAsync(recordId, version, "log.txt", "ok"u8.ToArray(), uploaderId);
        }
        Assert.Equal("Nadia Author", uploaded.UploadedByName);
        Assert.Null(uploaded.VoidedByName);

        await using (var assertBefore = Context(voiderId))
        {
            var detailBefore = await Queries(assertBefore).GetDetailAsync(recordId, voiderId);
            var attBefore = Assert.Single(detailBefore.Attachments);
            Assert.Equal("Nadia Author", attBefore.UploadedByName);
            Assert.Null(attBefore.VoidedByName);
        }

        QualityAttachmentDto voided;
        await using (var actVoid = Context(voiderId))
        {
            var currentVersion = await actVoid.QualityRecords.AsNoTracking()
                .Where(r => r.Id == recordId).Select(r => r.Version).SingleAsync();
            voided = await Attachments(actVoid, storage).VoidAsync(recordId, uploaded.Id, currentVersion, "Wrong file", voiderId);
        }
        Assert.Equal("Nadia Author", voided.UploadedByName);
        Assert.Equal("Omar Approver", voided.VoidedByName);

        await using var assertAfter = Context(uploaderId);
        var detailAfter = await Queries(assertAfter).GetDetailAsync(recordId, uploaderId);
        var attAfter = Assert.Single(detailAfter.Attachments);
        Assert.Equal("Nadia Author", attAfter.UploadedByName);
        Assert.Equal("Omar Approver", attAfter.VoidedByName);

        var coreDto = await Core(assertAfter).ReadDtoAsync(recordId, uploaderId);
        var coreAtt = Assert.Single(coreDto.Attachments);
        Assert.Equal("Nadia Author", coreAtt.UploadedByName);
        Assert.Equal("Omar Approver", coreAtt.VoidedByName);
    }

    [PostgresFact]
    public async Task Action_IsOverdue_Respects_Open_PlanApproved_Done_And_LabToday()
    {
        var today = DateOnly.FromDateTime(NowUtc.Date);
        int userId;
        int capaPlanId, capaImplId, ccProposedId, ccApprovedId, capaCancelledId;

        await using (var arrange = Context())
        {
            var sectionId = fixture.SeededSectionId;
            var deptId = fixture.SeededDepartmentId;

            userId = await CreateUserAsync(arrange, "Tariq Action", RoleType.SectionHead, deptId, sectionId,
                PermissionConstants.QualityView, PermissionConstants.QualityRaise,
                PermissionConstants.QualityLabConclude, PermissionConstants.QualityApprove);
            arrange.CurrentUserId = userId;
            var core = Core(arrange);

            async Task<int> MakeCapa(CapaStatus status, bool cancelled = false)
            {
                var r = await core.CreateAsync(QualityRecordType.Capa,
                    new($"CAPA {status}", "d", sectionId, null), userId);
                r.IsCancelled = cancelled;
                arrange.CapaDetails.Add(new CapaDetail { RecordId = r.Id, Status = status });
                arrange.QualityActions.AddRange(
                    new QualityAction
                    {
                        RecordId = r.Id, Kind = QualityActionKind.Corrective, Description = "Past open",
                        ResponsibleUserId = userId, DueDate = today.AddDays(-1)
                    },
                    new QualityAction
                    {
                        RecordId = r.Id, Kind = QualityActionKind.Corrective, Description = "Past done",
                        ResponsibleUserId = userId, DueDate = today.AddDays(-1), DoneAtUtc = NowUtc, DoneByUserId = userId
                    },
                    new QualityAction
                    {
                        RecordId = r.Id, Kind = QualityActionKind.Corrective, Description = "Today open",
                        ResponsibleUserId = userId, DueDate = today
                    });
                await arrange.SaveChangesAsync();
                return r.Id;
            }

            async Task<int> MakeCc(ChangeControlStatus status)
            {
                var r = await core.CreateAsync(QualityRecordType.ChangeControl,
                    new($"CC {status}", "d", sectionId, null), userId);
                arrange.ChangeControlDetails.Add(new ChangeControlDetail
                {
                    RecordId = r.Id, Status = status, Reason = "Update"
                });
                arrange.QualityActions.Add(new QualityAction
                {
                    RecordId = r.Id, Kind = QualityActionKind.ChangeTask, Description = "CC past open",
                    ResponsibleUserId = userId, DueDate = today.AddDays(-2)
                });
                await arrange.SaveChangesAsync();
                return r.Id;
            }

            capaPlanId = await MakeCapa(CapaStatus.Plan);
            capaImplId = await MakeCapa(CapaStatus.PlanApproved);
            capaCancelledId = await MakeCapa(CapaStatus.PlanApproved, cancelled: true);
            ccProposedId = await MakeCc(ChangeControlStatus.Proposed);
            ccApprovedId = await MakeCc(ChangeControlStatus.ApprovedForImplementation);
        }

        await using var assertDb = Context(userId);
        var q = Queries(assertDb);

        var plan = await q.GetDetailAsync(capaPlanId, userId);
        Assert.All(plan.Actions, a => Assert.False(a.IsOverdue));

        var impl = await q.GetDetailAsync(capaImplId, userId);
        Assert.True(impl.Actions.Single(a => a.Description == "Past open").IsOverdue);
        Assert.False(impl.Actions.Single(a => a.Description == "Past done").IsOverdue);
        Assert.False(impl.Actions.Single(a => a.Description == "Today open").IsOverdue);

        var cancelled = await q.GetDetailAsync(capaCancelledId, userId);
        Assert.All(cancelled.Actions, a => Assert.False(a.IsOverdue));

        var ccProp = await q.GetDetailAsync(ccProposedId, userId);
        Assert.False(Assert.Single(ccProp.Actions).IsOverdue);

        var ccApp = await q.GetDetailAsync(ccApprovedId, userId);
        Assert.True(Assert.Single(ccApp.Actions).IsOverdue);
    }

    [PostgresFact]
    public async Task Links_Endpoint_Returns_PagedResult_With_TotalCount()
    {
        var (sectionId, authorId, _, recordId, version) = await SeedDeviationAsync();

        await using (var arrange = Context(authorId))
        {
            var targets = Enumerable.Range(1, 3).Select(i => new QualityRecord
            {
                Number = $"CAPA-LNK-{Guid.NewGuid():N}"[..20],
                Type = QualityRecordType.Capa,
                Title = $"Target {i}",
                Description = "d",
                SectionId = sectionId,
                RaisedByUserId = authorId,
                RaisedAtUtc = NowUtc
            }).ToList();
            arrange.QualityRecords.AddRange(targets);
            await arrange.SaveChangesAsync();

            var linkService = Links(arrange);
            foreach (var t in targets)
            {
                var currentVersion = await arrange.QualityRecords.AsNoTracking()
                    .Where(r => r.Id == recordId).Select(r => r.Version).SingleAsync();
                await linkService.AddAsync(recordId, QualityLinkTargetType.QualityRecord, t.Id, authorId, currentVersion);
            }
        }

        await using var assertDb = Context(authorId);
        var controller = new QualityRecordsController(
            Core(assertDb),
            Links(assertDb),
            Attachments(assertDb, new MemoryStorage()),
            Comments(assertDb),
            Queries(assertDb),
            new QualityReportBuilder(Queries(assertDb), Links(assertDb), new DummyPdf(), new DataExportAuditService(assertDb), assertDb))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, authorId.ToString(System.Globalization.CultureInfo.InvariantCulture))],
                        "Test"))
                }
            }
        };

        var page1Result = await controller.ResolveLinks(recordId, offset: 0, pageSize: 2);
        var ok1 = Assert.IsType<OkObjectResult>(page1Result);
        var env1 = Assert.IsType<ApiResponse<PagedResult<QualityLinkDto>>>(ok1.Value);
        Assert.Equal(2, env1.Data!.Items.Count);
        Assert.Equal(3, env1.Data.TotalCount);

        var page2Result = await controller.ResolveLinks(recordId, offset: 2, pageSize: 2);
        var ok2 = Assert.IsType<OkObjectResult>(page2Result);
        var env2 = Assert.IsType<ApiResponse<PagedResult<QualityLinkDto>>>(ok2.Value);
        Assert.Single(env2.Data!.Items);
        Assert.Equal(3, env2.Data.TotalCount);
    }
}
