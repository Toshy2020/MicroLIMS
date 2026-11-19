using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class NotificationCentrePostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public NotificationCentrePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 30)];

    private static ControllerContext ContextFor(int userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "Test"))
        }
    };

    private static NotificationsController CreateNotificationsController(
        MicroLimsDbContext db,
        int userId,
        TimeProvider? time = null) =>
        new(new NotificationQueryService(db, time, new PermissionService(db)))
        {
            ControllerContext = ContextFor(userId)
        };

    private static NotificationPreferencesController CreatePreferencesController(
        MicroLimsDbContext db,
        int userId,
        TimeProvider? time = null)
    {
        db.CurrentUserId = userId;
        return new NotificationPreferencesController(
            new NotificationPreferencesService(db, new UserSectionScopeService(db), new PermissionService(db), time))
        {
            ControllerContext = ContextFor(userId)
        };
    }

    private static AdminEmailOutboxController CreateOutboxController(
        MicroLimsDbContext db,
        int userId,
        TimeProvider? time = null) =>
        new(new NotificationQueryService(db, time, new PermissionService(db)))
        {
            ControllerContext = ContextFor(userId)
        };

    private async Task<DocumentSection> CreateSectionAsync(MicroLimsDbContext db, string tag)
    {
        var section = new DocumentSection
        {
            DepartmentId = _fixture.SeededDepartmentId,
            Code = UniqueCode($"NC{tag}_"),
            Name = $"Notif Centre {tag} {Guid.NewGuid():N}"[..36],
            IsActive = true
        };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    private async Task<User> CreateUserAsync(
        MicroLimsDbContext db,
        RoleType roleType,
        int? sectionId = null,
        string? email = null,
        bool isActive = true)
    {
        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == roleType);
        var user = new User
        {
            FullName = $"NC {roleType} {Guid.NewGuid():N}"[..30],
            Username = UniqueCode($"nc_{roleType}_").ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash("NotifCentre-123!"),
            RoleId = role.Id,
            Email = email,
            IsActive = isActive
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (sectionId.HasValue)
        {
            db.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = user.Id,
                DepartmentId = _fixture.SeededDepartmentId,
                SectionId = sectionId.Value
            });
            await db.SaveChangesAsync();
        }

        return user;
    }

    [PostgresFact]
    public async Task List_OnlyMine_FilterByStateTypeSection_Paged_ClampedTo100()
    {
        var baseTime = new DateTime(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);
        int userAId;
        int userBId;
        int section1Id;
        int section2Id;
        string section1Name;
        var searchMarker = $"needle-{Guid.NewGuid():N}"[..16];

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var sec1 = await CreateSectionAsync(arrangeDb, "A");
            var sec2 = await CreateSectionAsync(arrangeDb, "B");
            section1Id = sec1.Id;
            section2Id = sec2.Id;
            section1Name = sec1.Name;

            var userA = await CreateUserAsync(arrangeDb, RoleType.Analyst, section1Id);
            var userB = await CreateUserAsync(arrangeDb, RoleType.Analyst, section2Id);
            userAId = userA.Id;
            userBId = userB.Id;

            // 105 unread notifications for User A in section 1 (ReviewWaiting)
            for (var i = 0; i < 105; i++)
            {
                arrangeDb.NotificationLogs.Add(new NotificationLog
                {
                    UserId = userAId,
                    Type = "ReviewWaiting",
                    Title = "Sample awaiting review",
                    Message = i == 0 ? $"Sample {searchMarker} waiting" : $"Sample S-{i:D3} waiting",
                    Severity = "info",
                    TargetType = "sample",
                    TargetId = 1000 + i,
                    SampleId = 1000 + i,
                    TestOrderId = 2000 + i,
                    SectionId = section1Id,
                    IsRead = false,
                    CreatedAt = baseTime.AddMinutes(i)
                });
            }

            // 1 read (unresolved) notification for User A in section 2 (MediaExpiry)
            arrangeDb.NotificationLogs.Add(new NotificationLog
            {
                UserId = userAId,
                Type = "MediaExpiry",
                Title = "Media lot expiring or expired",
                Message = "Media lot M-10 expiring",
                Severity = "warning",
                TargetType = "media",
                TargetId = 55,
                SectionId = section2Id,
                IsRead = true,
                ReadAt = baseTime.AddHours(3),
                CreatedAt = baseTime.AddHours(3)
            });

            // 1 done (resolved) notification for User A in section 1 (TestAssigned)
            arrangeDb.NotificationLogs.Add(new NotificationLog
            {
                UserId = userAId,
                Type = "TestAssigned",
                Title = "Test assigned",
                Message = "Test assigned and finished",
                Severity = "info",
                TargetType = "testOrder",
                TargetId = 77,
                SampleId = 70,
                TestOrderId = 77,
                SectionId = section1Id,
                IsRead = false,
                ResolvedAt = baseTime.AddHours(4),
                CreatedAt = baseTime.AddHours(4)
            });

            // User B notification (must never appear in User A's list)
            arrangeDb.NotificationLogs.Add(new NotificationLog
            {
                UserId = userBId,
                Type = "ReviewWaiting",
                Title = "Sample awaiting review",
                Message = $"Other user {searchMarker}",
                Severity = "info",
                SectionId = section1Id,
                IsRead = false,
                CreatedAt = baseTime.AddHours(5)
            });

            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        var controller = CreateNotificationsController(actDb, userAId);

        // Page size > 100 clamped to 100
        var allPage1 = Assert.IsType<ApiResponse<PagedResult<MicroLIMS.Application.Services.Notifications.NotificationDto>>>(
            Assert.IsType<OkObjectResult>(await controller.List(state: "all", page: 1, pageSize: 500)).Value).Data!;
        Assert.Equal(100, allPage1.PageSize);
        Assert.Equal(107, allPage1.TotalCount);
        Assert.Equal(100, allPage1.Items.Count);
        Assert.DoesNotContain(allPage1.Items, x => x.Message.Contains("Other user", StringComparison.Ordinal));

        var allPage2 = Assert.IsType<ApiResponse<PagedResult<MicroLIMS.Application.Services.Notifications.NotificationDto>>>(
            Assert.IsType<OkObjectResult>(await controller.List(state: "all", page: 2, pageSize: 500)).Value).Data!;
        Assert.Equal(7, allPage2.Items.Count);

        // State filters
        var unread = Assert.IsType<ApiResponse<PagedResult<MicroLIMS.Application.Services.Notifications.NotificationDto>>>(
            Assert.IsType<OkObjectResult>(await controller.List(state: "unread", page: 1, pageSize: 200)).Value).Data!;
        Assert.Equal(105, unread.TotalCount);
        Assert.All(unread.Items, item =>
        {
            Assert.Null(item.ReadAt);
            Assert.Null(item.ResolvedAt);
        });

        var read = Assert.IsType<ApiResponse<PagedResult<MicroLIMS.Application.Services.Notifications.NotificationDto>>>(
            Assert.IsType<OkObjectResult>(await controller.List(state: "read")).Value).Data!;
        var readItem = Assert.Single(read.Items);
        Assert.Equal("MediaExpiry", readItem.Type);
        Assert.NotNull(readItem.ReadAt);
        Assert.Null(readItem.ResolvedAt);

        var done = Assert.IsType<ApiResponse<PagedResult<MicroLIMS.Application.Services.Notifications.NotificationDto>>>(
            Assert.IsType<OkObjectResult>(await controller.List(state: "done")).Value).Data!;
        var doneItem = Assert.Single(done.Items);
        Assert.Equal("TestAssigned", doneItem.Type);
        Assert.NotNull(doneItem.ResolvedAt);
        Assert.Equal(section1Name, doneItem.SectionName);
        Assert.Equal("testOrder", doneItem.Target.Type);
        Assert.Equal(77, doneItem.Target.Id);
        Assert.Equal(70, doneItem.Target.SampleId);
        Assert.Equal(77, doneItem.Target.TestOrderId);

        // Type + section + search filter
        var searched = Assert.IsType<ApiResponse<PagedResult<MicroLIMS.Application.Services.Notifications.NotificationDto>>>(
            Assert.IsType<OkObjectResult>(await controller.List(state: "unread", type: "ReviewWaiting", sectionId: section1Id, q: searchMarker.ToUpperInvariant())).Value).Data!;
        var match = Assert.Single(searched.Items);
        Assert.Equal(section1Name, match.SectionName);
        Assert.Contains(searchMarker, match.Message, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task MarkRead_SetsReadAt()
    {
        var fixedNow = new DateTimeOffset(2026, 11, 15, 14, 30, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(fixedNow);
        int userId;
        int notifId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var user = await CreateUserAsync(arrangeDb, RoleType.Analyst);
            userId = user.Id;
            var row = new NotificationLog
            {
                UserId = userId,
                Type = "TestAssigned",
                Title = "Test assigned",
                Message = "Please run test",
                Severity = "info",
                IsRead = false,
                CreatedAt = fixedNow.AddMinutes(-10).UtcDateTime
            };
            arrangeDb.NotificationLogs.Add(row);
            await arrangeDb.SaveChangesAsync();
            notifId = row.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var controller = CreateNotificationsController(actDb, userId, clock);
            Assert.IsType<OkObjectResult>(await controller.MarkRead(notifId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.NotificationLogs.AsNoTracking().SingleAsync(n => n.Id == notifId);
        Assert.True(persisted.IsRead);
        Assert.Equal(fixedNow.UtcDateTime, persisted.ReadAt);
    }

    [PostgresFact]
    public async Task MarkRead_OtherUsersNotification_404()
    {
        var fixedNow = new DateTimeOffset(2026, 11, 15, 15, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(fixedNow);
        int ownerId;
        int otherUserId;
        int notifId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            ownerId = (await CreateUserAsync(arrangeDb, RoleType.Analyst)).Id;
            otherUserId = (await CreateUserAsync(arrangeDb, RoleType.Analyst)).Id;
            var row = new NotificationLog
            {
                UserId = ownerId,
                Type = "ReviewWaiting",
                Title = "Sample awaiting review",
                Message = "Owner private notification",
                Severity = "info",
                IsRead = false,
                CreatedAt = fixedNow.AddMinutes(-5).UtcDateTime
            };
            arrangeDb.NotificationLogs.Add(row);
            await arrangeDb.SaveChangesAsync();
            notifId = row.Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var controller = CreateNotificationsController(actDb, otherUserId, clock);
            await Assert.ThrowsAsync<NotFoundException>(() => controller.MarkRead(notifId));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.NotificationLogs.AsNoTracking().SingleAsync(n => n.Id == notifId);
        Assert.False(persisted.IsRead);
        Assert.Null(persisted.ReadAt);
    }

    [PostgresFact]
    public async Task Summary_CountsUnreadUnresolvedOnly()
    {
        var now = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        int userId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = (await CreateUserAsync(arrangeDb, RoleType.Analyst)).Id;
            var otherUserId = (await CreateUserAsync(arrangeDb, RoleType.Analyst)).Id;

            arrangeDb.NotificationLogs.AddRange(
                new NotificationLog { UserId = userId, Type = "ReviewWaiting", Message = "Unread unresolved 1", IsRead = false, ResolvedAt = null, CreatedAt = now },
                new NotificationLog { UserId = userId, Type = "ReviewWaiting", Message = "Unread unresolved 2", IsRead = false, ResolvedAt = null, CreatedAt = now },
                new NotificationLog { UserId = userId, Type = "ReviewWaiting", Message = "Read unresolved", IsRead = true, ReadAt = now, ResolvedAt = null, CreatedAt = now },
                new NotificationLog { UserId = userId, Type = "ReviewWaiting", Message = "Unread resolved", IsRead = false, ResolvedAt = now, CreatedAt = now },
                new NotificationLog { UserId = userId, Type = "ReviewWaiting", Message = "Read resolved", IsRead = true, ReadAt = now, ResolvedAt = now, CreatedAt = now },
                new NotificationLog { UserId = otherUserId, Type = "ReviewWaiting", Message = "Other user", IsRead = false, ResolvedAt = null, CreatedAt = now });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        var controller = CreateNotificationsController(actDb, userId);
        var ok = Assert.IsType<OkObjectResult>(await controller.Summary());
        var summary = Assert.IsType<ApiResponse<NotificationSummaryDto>>(ok.Value).Data!;
        Assert.Equal(2, summary.Unread);
    }

    [PostgresFact]
    public async Task Preference_DefaultImmediateForEmailTypes()
    {
        int userId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var sec = await CreateSectionAsync(arrangeDb, "DEF");
            userId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, sec.Id)).Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var controller = CreatePreferencesController(actDb, userId);
        var ok = Assert.IsType<OkObjectResult>(await controller.Get());
        var rows = Assert.IsType<ApiResponse<IReadOnlyList<NotificationPreferenceRowDto>>>(ok.Value).Data!;

        Assert.Equal(NotificationCatalog.Types.Count, rows.Count);
        foreach (var row in rows)
        {
            Assert.False(row.Mandatory);
            Assert.Null(row.MandatoryReason);
            if (row.EmailAllowed)
            {
                Assert.Equal(EmailMode.Immediate, row.EmailMode);
            }
            else
            {
                Assert.Equal(EmailMode.Off, row.EmailMode);
            }
        }
    }

    [PostgresFact]
    public async Task Preference_SetDigest_StopsImmediateOutboxRows()
    {
        var now = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        int userId;
        int sectionId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            sectionId = (await CreateSectionAsync(arrangeDb, "DIG")).Id;
            userId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, sectionId, email: "digest.user@lab.test")).Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var controller = CreatePreferencesController(actDb, userId, clock);
            var ok = Assert.IsType<OkObjectResult>(await controller.Update(
                new UpdateNotificationPreferencesRequest(new[]
                {
                    new UpdateNotificationPreferenceItem("MediaExpiry", EmailMode.Digest)
                })));
            var rows = Assert.IsType<ApiResponse<IReadOnlyList<NotificationPreferenceRowDto>>>(ok.Value).Data!;
            Assert.Equal(EmailMode.Digest, rows.Single(r => r.Type == "MediaExpiry").EmailMode);
        }

        await using (var publishDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(publishDb, clock);
            await publisher.PublishAsync(new NotificationRequest(
                "MediaExpiry",
                new[] { userId },
                "Media lot expiring soon",
                "media",
                123,
                sectionId,
                $"MediaExpiry:media:123:{Guid.NewGuid():N}"));
            await publishDb.SaveChangesAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        var pref = await assertDb.UserNotificationPreferences.AsNoTracking()
            .SingleAsync(p => p.UserId == userId && p.Type == "MediaExpiry");
        Assert.Equal(EmailMode.Digest, pref.EmailMode);
        Assert.True(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(UserNotificationPreference) && a.UserId == userId));
        Assert.True(await assertDb.NotificationLogs.AsNoTracking()
            .AnyAsync(n => n.UserId == userId && n.Type == "MediaExpiry"));
        Assert.False(await assertDb.EmailOutbox.AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.Kind == EmailOutboxKind.Immediate));
    }

    [PostgresFact]
    public async Task Mandatory_OverridesUserOff()
    {
        var now = new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        int analystId;
        int headId;
        int sectionId;
        string sectionName;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var sec = await CreateSectionAsync(arrangeDb, "MND");
            sectionId = sec.Id;
            sectionName = sec.Name;
            analystId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, sectionId, email: "analyst.mand@lab.test")).Id;
            headId = (await CreateUserAsync(arrangeDb, RoleType.SectionHead, sectionId)).Id;
        }

        // User turns MediaExpiry email Off
        await using (var userActDb = _fixture.CreateDbContext())
        {
            var userCtrl = CreatePreferencesController(userActDb, analystId, clock);
            Assert.IsType<OkObjectResult>(await userCtrl.Update(
                new UpdateNotificationPreferencesRequest(new[]
                {
                    new UpdateNotificationPreferenceItem("MediaExpiry", EmailMode.Off)
                })));
        }

        // Section Head marks MediaExpiry mandatory for that section
        await using (var headActDb = _fixture.CreateDbContext())
        {
            var headCtrl = CreatePreferencesController(headActDb, headId, clock);
            var ok = Assert.IsType<OkObjectResult>(await headCtrl.SetMandatory(
                sectionId,
                new UpdateMandatoryNotificationTypesRequest(new[] { "MediaExpiry" })));
            var mandatoryRows = Assert.IsType<ApiResponse<IReadOnlyList<MandatoryNotificationTypeDto>>>(ok.Value).Data!;
            Assert.True(mandatoryRows.Single(r => r.Type == "MediaExpiry").Mandatory);
        }

        // User sees MediaExpiry locked to Immediate with section reason, and changing it is refused
        await using (var userCheckDb = _fixture.CreateDbContext())
        {
            var userCtrl = CreatePreferencesController(userCheckDb, analystId, clock);
            var rows = Assert.IsType<ApiResponse<IReadOnlyList<NotificationPreferenceRowDto>>>(
                Assert.IsType<OkObjectResult>(await userCtrl.Get()).Value).Data!;
            var mediaRow = rows.Single(r => r.Type == "MediaExpiry");
            Assert.True(mediaRow.Mandatory);
            Assert.Equal(EmailMode.Immediate, mediaRow.EmailMode);
            Assert.Contains(sectionName, mediaRow.MandatoryReason ?? string.Empty, StringComparison.Ordinal);

            await Assert.ThrowsAsync<FieldValidationException>(() => userCtrl.Update(
                new UpdateNotificationPreferencesRequest(new[]
                {
                    new UpdateNotificationPreferenceItem("MediaExpiry", EmailMode.Off)
                })));
        }

        // Publishing MediaExpiry in that section still queues an Immediate email despite stored Off preference
        await using (var publishDb = _fixture.CreateDbContext())
        {
            var publisher = new NotificationPublisher(publishDb, clock);
            await publisher.PublishAsync(new NotificationRequest(
                "MediaExpiry",
                new[] { analystId },
                "Mandatory media alert",
                "media",
                456,
                sectionId,
                $"MediaExpiry:media:456:{Guid.NewGuid():N}"));
            await publishDb.SaveChangesAsync();
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.True(await assertDb.MandatoryNotificationTypes.AsNoTracking()
            .AnyAsync(m => m.SectionId == sectionId && m.Type == "MediaExpiry" && m.SetByUserId == headId));
        Assert.True(await assertDb.AuditLogs.AsNoTracking()
            .AnyAsync(a => a.EntityName == nameof(MandatoryNotificationType) && a.UserId == headId));
        Assert.True(await assertDb.EmailOutbox.AsNoTracking()
            .AnyAsync(e => e.UserId == analystId && e.Kind == EmailOutboxKind.Immediate));
    }

    [PostgresFact]
    public async Task Mandatory_SetWithoutPermissionOrForOtherSection_Refused()
    {
        var getMandatoryAuth = typeof(NotificationPreferencesController)
            .GetMethod(nameof(NotificationPreferencesController.GetMandatory))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var setMandatoryAuth = typeof(NotificationPreferencesController)
            .GetMethod(nameof(NotificationPreferencesController.SetMandatory))!
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(PermissionConstants.NotificationsConfigure, getMandatoryAuth?.Policy);
        Assert.Equal(PermissionConstants.NotificationsConfigure, setMandatoryAuth?.Policy);

        int sectionAId;
        int sectionBId;
        int analystInAId;
        int headInAId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            sectionAId = (await CreateSectionAsync(arrangeDb, "SA")).Id;
            sectionBId = (await CreateSectionAsync(arrangeDb, "SB")).Id;
            analystInAId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, sectionAId)).Id;
            headInAId = (await CreateUserAsync(arrangeDb, RoleType.SectionHead, sectionAId)).Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            // Analyst lacks Notifications.Configure
            var analystCtrl = CreatePreferencesController(actDb, analystInAId);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                analystCtrl.GetMandatory(sectionAId));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                analystCtrl.SetMandatory(sectionAId, new UpdateMandatoryNotificationTypesRequest(new[] { "MediaExpiry" })));

            // Section Head in A tries to configure Section B
            var headCtrl = CreatePreferencesController(actDb, headInAId);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                headCtrl.GetMandatory(sectionBId));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                headCtrl.SetMandatory(sectionBId, new UpdateMandatoryNotificationTypesRequest(new[] { "MediaExpiry" })));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.MandatoryNotificationTypes.AsNoTracking()
            .AnyAsync(m => m.SectionId == sectionAId || m.SectionId == sectionBId));
    }

    [PostgresFact]
    public async Task SetEmailModeOnNonEmailType_Refused()
    {
        int userId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            userId = (await CreateUserAsync(arrangeDb, RoleType.Analyst)).Id;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var controller = CreatePreferencesController(actDb, userId);
            await Assert.ThrowsAsync<FieldValidationException>(() => controller.Update(
                new UpdateNotificationPreferencesRequest(new[]
                {
                    new UpdateNotificationPreferenceItem("ReviewWaiting", EmailMode.Immediate)
                })));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.UserNotificationPreferences.AsNoTracking()
            .AnyAsync(p => p.UserId == userId && p.Type == "ReviewWaiting"));
    }

    [PostgresFact]
    public async Task OutboxSummary_RequiresUsersManage_CountsWaitingAndFailed()
    {
        var classAuth = typeof(AdminEmailOutboxController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(PermissionConstants.UsersManage, classAuth?.Policy);

        var t1 = new DateTime(2026, 11, 15, 7, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 11, 15, 11, 45, 0, DateTimeKind.Utc);
        int adminId;
        int analystId;
        int noEmailActiveId;
        int withEmailActiveId;
        int noEmailInactiveId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            await arrangeDb.EmailOutbox.ExecuteDeleteAsync();

            adminId = (await CreateUserAsync(arrangeDb, RoleType.SystemAdministrator, email: "admin@lab.test")).Id;
            analystId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, email: "analyst@lab.test")).Id;
            noEmailActiveId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, email: null, isActive: true)).Id;
            withEmailActiveId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, email: "has.email@lab.test", isActive: true)).Id;
            noEmailInactiveId = (await CreateUserAsync(arrangeDb, RoleType.Analyst, email: "", isActive: false)).Id;

            arrangeDb.EmailOutbox.AddRange(
                new EmailOutbox
                {
                    UserId = withEmailActiveId,
                    ToAddress = "has.email@lab.test",
                    Subject = "Waiting 1",
                    Body = "Body 1",
                    Kind = EmailOutboxKind.Immediate,
                    CreatedAt = t1,
                    NextAttemptAt = t1,
                    Attempts = 0
                },
                new EmailOutbox
                {
                    UserId = withEmailActiveId,
                    ToAddress = "has.email@lab.test",
                    Subject = "Waiting 2",
                    Body = "Body 2",
                    Kind = EmailOutboxKind.Digest,
                    CreatedAt = t1,
                    NextAttemptAt = t1,
                    Attempts = 1
                },
                new EmailOutbox
                {
                    UserId = withEmailActiveId,
                    ToAddress = "has.email@lab.test",
                    Subject = "Failed 1",
                    Body = "Body 3",
                    Kind = EmailOutboxKind.Immediate,
                    CreatedAt = t1,
                    NextAttemptAt = t1,
                    Attempts = 5,
                    FailedAt = t1.AddMinutes(30),
                    LastError = "SMTP 550"
                },
                new EmailOutbox
                {
                    UserId = withEmailActiveId,
                    ToAddress = "has.email@lab.test",
                    Subject = "Sent 1",
                    Body = "Body 4",
                    Kind = EmailOutboxKind.Immediate,
                    CreatedAt = t1,
                    NextAttemptAt = t1,
                    Attempts = 1,
                    SentAt = t1.AddMinutes(1)
                },
                new EmailOutbox
                {
                    UserId = withEmailActiveId,
                    ToAddress = "has.email@lab.test",
                    Subject = "Sent 2",
                    Body = "Body 5",
                    Kind = EmailOutboxKind.Immediate,
                    CreatedAt = t2,
                    NextAttemptAt = t2,
                    Attempts = 1,
                    SentAt = t2
                });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        var unauthorizedCtrl = CreateOutboxController(actDb, analystId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => unauthorizedCtrl.Summary());

        var adminCtrl = CreateOutboxController(actDb, adminId);
        var ok = Assert.IsType<OkObjectResult>(await adminCtrl.Summary());
        var summary = Assert.IsType<ApiResponse<EmailOutboxSummaryDto>>(ok.Value).Data!;

        Assert.Equal(2, summary.Waiting);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(t2, summary.LastSentAt);
        Assert.Contains(summary.UsersWithoutEmail, u => u.Id == noEmailActiveId);
        Assert.DoesNotContain(summary.UsersWithoutEmail, u => u.Id == withEmailActiveId);
        Assert.DoesNotContain(summary.UsersWithoutEmail, u => u.Id == noEmailInactiveId);
    }
}
