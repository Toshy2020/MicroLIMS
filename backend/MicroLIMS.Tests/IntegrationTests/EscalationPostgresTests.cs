using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MicroLIMS.API.Configuration;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class EscalationPostgresTests
{
    private static readonly TimeZoneInfo CairoZone = LabClock.ResolveTimeZone("Africa/Cairo");
    private readonly PostgresTestFixture _fixture;

    public EscalationPostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static DateTime CairoUtc(int year, int month, int day, int hour, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, CairoZone);
    }

    private static string UniqueCode(string prefix) =>
        $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 8, 24)];

    private static IConfiguration BuildTestConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Lab:TimeZoneId"] = "Africa/Cairo",
                ["Notifications:AppBaseUrl"] = "http://localhost:5173"
            })
            .Build();

    private static EscalationService CreateService(MicroLimsDbContext db, TimeProvider time)
    {
        var config = BuildTestConfig();
        var clock = new LabClock(time, CairoZone);
        var publisher = new NotificationPublisher(db, time, config, clock);
        var recipients = new NotificationRecipients(db, new SegregationOfDutiesGuard(db));
        var permissions = new PermissionService(db);
        var scope = new UserSectionScopeService(db);
        return new EscalationService(db, publisher, recipients, permissions, scope, clock, time);
    }

    private async Task EnsureDefaultWorkingHoursAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var existing = await db.LabWorkingHours.ToDictionaryAsync(h => h.DayOfWeek);
        var days = Enum.GetValues<DayOfWeek>();
        foreach (var day in days)
        {
            var isWorking = day is not (DayOfWeek.Friday or DayOfWeek.Saturday);
            if (existing.TryGetValue(day, out var row))
            {
                row.IsWorkingDay = isWorking;
                row.StartMinute = isWorking ? 8 * 60 : 0;
                row.EndMinute = isWorking ? 16 * 60 : 0;
            }
            else
            {
                db.LabWorkingHours.Add(new LabWorkingHours
                {
                    DayOfWeek = day,
                    IsWorkingDay = isWorking,
                    StartMinute = isWorking ? 8 * 60 : 0,
                    EndMinute = isWorking ? 16 * 60 : 0
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task CleanupRulesAndDatesAsync(IEnumerable<int> ruleIds, IEnumerable<int>? calendarDateIds = null)
    {
        var ids = ruleIds.Where(id => id > 0).Distinct().ToList();
        var dateIds = calendarDateIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0 && dateIds.Count == 0)
            return;

        await using var db = _fixture.CreateDbContext();
        if (ids.Count > 0)
        {
            var rules = await db.EscalationRules.Where(r => ids.Contains(r.Id) && r.IsActive).ToListAsync();
            foreach (var r in rules)
            {
                r.IsActive = false;
                r.RetiredAt = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                r.RetiredByUserId = _fixture.SeededUserId;
            }
        }
        if (dateIds.Count > 0)
        {
            var dates = await db.LabCalendarDates.Where(d => dateIds.Contains(d.Id)).ToListAsync();
            db.LabCalendarDates.RemoveRange(dates);
        }
        await db.SaveChangesAsync();
    }

    private async Task<DocumentSection> SeedSectionAsync(string tag)
    {
        await using var db = _fixture.CreateDbContext();
        var section = new DocumentSection
        {
            DepartmentId = _fixture.SeededDepartmentId,
            Code = UniqueCode($"ES{tag}_"),
            Name = $"Escalation Sec {tag} {Guid.NewGuid():N}"[..36],
            IsActive = true
        };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    private async Task<User> SeedUserInSectionAsync(RoleType roleType, int? sectionId, string? email = null)
    {
        await using var db = _fixture.CreateDbContext();
        var roleId = await db.Roles
            .AsNoTracking()
            .Where(r => r.Type == roleType && r.IsActive)
            .OrderByDescending(r => r.IsSystemRole)
            .ThenBy(r => r.Id)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync();
        if (roleId is null)
        {
            var role = new Role { Type = roleType, Name = roleType.ToString(), IsSystemRole = true, IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            roleId = role.Id;
        }

        var username = UniqueCode($"esc_{roleType.ToString().ToLowerInvariant()}_");
        var user = new User
        {
            FullName = $"Test {roleType} {Guid.NewGuid():N}"[..32],
            Username = username,
            PasswordHash = TestPasswords.Hash("Esc-Pass-123!"),
            RoleId = roleId.Value,
            Email = email ?? $"{username}@example.test",
            IsActive = true
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

    private async Task<CauseOfTesting> EnsureCauseAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var existing = await db.CausesOfTesting.AsNoTracking().FirstOrDefaultAsync();
        if (existing is not null)
            return existing;

        var cause = new CauseOfTesting { Name = $"Routine {Guid.NewGuid():N}"[..24], IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        return cause;
    }

    [PostgresFact]
    public async Task Create_Audited()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("AUD");
        var creator = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var now = new DateTimeOffset(CairoUtc(2026, 11, 15, 10, 0), TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var createdRuleIds = new List<int>();

        try
        {
            EscalationRuleDto created;
            await using (var actDb = _fixture.CreateDbContext())
            {
                var service = CreateService(actDb, clock);
                created = await service.CreateAsync(
                    creator.Id,
                    section.Id,
                    EscalationTrigger.ReviewWaiting,
                    8m,
                    RoleType.SectionHead);
                createdRuleIds.Add(created.Id);
            }

            await using var assertDb = _fixture.CreateDbContext();
            var stored = await assertDb.EscalationRules
                .AsNoTracking()
                .SingleAsync(r => r.Id == created.Id);
            Assert.Equal(section.Id, stored.SectionId);
            Assert.Equal(EscalationTrigger.ReviewWaiting, stored.Trigger);
            Assert.Equal(8m, stored.ThresholdWorkingHours);
            Assert.Equal(RoleType.SectionHead, stored.NotifyRoleType);
            Assert.True(stored.IsActive);
            Assert.Equal(creator.Id, stored.CreatedByUserId);
            Assert.Null(stored.RetiredAt);

            var audit = await assertDb.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityName == nameof(EscalationRule) && a.EntityId == created.Id.ToString())
                .SingleAsync();
            Assert.Equal("Create", audit.Action);
            Assert.Equal(creator.Id, audit.UserId);
        }
        finally
        {
            await CleanupRulesAndDatesAsync(createdRuleIds);
        }
    }

    [PostgresFact]
    public async Task Create_WithoutWorkingHours_Refused()
    {
        var section = await SeedSectionAsync("NOWH");
        var creator = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var clock = new MutableTimeProvider(new DateTimeOffset(CairoUtc(2026, 11, 15, 10, 0), TimeSpan.Zero));

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var rows = await arrangeDb.LabWorkingHours.ToListAsync();
                foreach (var row in rows)
                {
                    row.IsWorkingDay = false;
                    row.StartMinute = 0;
                    row.EndMinute = 0;
                }
                await arrangeDb.SaveChangesAsync();
            }

            await using (var actDb = _fixture.CreateDbContext())
            {
                var service = CreateService(actDb, clock);
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.CreateAsync(creator.Id, section.Id, EscalationTrigger.ReviewWaiting, 8m, RoleType.SectionHead));
                Assert.Contains("Set the laboratory working hours first.", ex.Message);
            }

            await using var assertDb = _fixture.CreateDbContext();
            Assert.False(await assertDb.EscalationRules.AsNoTracking().AnyAsync(r => r.SectionId == section.Id));
        }
        finally
        {
            await EnsureDefaultWorkingHoursAsync();
        }
    }

    [PostgresFact]
    public async Task Create_ForSectionIAmNotIn_Refused()
    {
        await EnsureDefaultWorkingHoursAsync();
        var mySection = await SeedSectionAsync("MY");
        var otherSection = await SeedSectionAsync("OTH");
        var creator = await SeedUserInSectionAsync(RoleType.SectionHead, mySection.Id);
        var clock = new MutableTimeProvider(new DateTimeOffset(CairoUtc(2026, 11, 15, 10, 0), TimeSpan.Zero));

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = CreateService(actDb, clock);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.CreateAsync(creator.Id, otherSection.Id, EscalationTrigger.ReviewWaiting, 8m, RoleType.SectionHead));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.EscalationRules.AsNoTracking().AnyAsync(r => r.SectionId == otherSection.Id));
    }

    [PostgresFact]
    public async Task Create_DuplicateActiveRule_Refused()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("DUP");
        var creator = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var clock = new MutableTimeProvider(new DateTimeOffset(CairoUtc(2026, 11, 15, 10, 0), TimeSpan.Zero));
        var createdRuleIds = new List<int>();

        try
        {
            await using (var actDb = _fixture.CreateDbContext())
            {
                var service = CreateService(actDb, clock);
                var first = await service.CreateAsync(creator.Id, section.Id, EscalationTrigger.ApprovalWaiting, 6m, RoleType.QcManager);
                createdRuleIds.Add(first.Id);
            }

            await using (var actDb2 = _fixture.CreateDbContext())
            {
                var service = CreateService(actDb2, clock);
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    service.CreateAsync(creator.Id, section.Id, EscalationTrigger.ApprovalWaiting, 12m, RoleType.QcManager));
            }
        }
        finally
        {
            await CleanupRulesAndDatesAsync(createdRuleIds);
        }
    }

    [PostgresFact]
    public async Task Threshold0or1000_Refused()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("THR");
        var creator = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var clock = new MutableTimeProvider(new DateTimeOffset(CairoUtc(2026, 11, 15, 10, 0), TimeSpan.Zero));

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb, clock);

        var ex0 = await Assert.ThrowsAsync<FieldValidationException>(() =>
            service.CreateAsync(creator.Id, section.Id, EscalationTrigger.ReviewWaiting, 0m, RoleType.SectionHead));
        Assert.Contains(ex0.FieldErrors, e => e.Field == "thresholdWorkingHours");

        var ex1000 = await Assert.ThrowsAsync<FieldValidationException>(() =>
            service.CreateAsync(creator.Id, section.Id, EscalationTrigger.ReviewWaiting, 1000m, RoleType.SectionHead));
        Assert.Contains(ex1000.FieldErrors, e => e.Field == "thresholdWorkingHours");
    }

    [PostgresFact]
    public async Task Replace_RetiresOld_CreatesNewLinked_OldStillListedWithIncludeRetired()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("REP");
        var creator = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var t0 = new DateTimeOffset(CairoUtc(2026, 11, 15, 10, 0), TimeSpan.Zero);
        var t1 = new DateTimeOffset(CairoUtc(2026, 11, 15, 12, 0), TimeSpan.Zero);
        var clock = new MutableTimeProvider(t0);
        var ruleIds = new List<int>();

        try
        {
            EscalationRuleDto original;
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var service = CreateService(arrangeDb, clock);
                original = await service.CreateAsync(creator.Id, section.Id, EscalationTrigger.ReviewWaiting, 8m, RoleType.SectionHead);
                ruleIds.Add(original.Id);
            }

            clock.Now = t1;
            EscalationRuleDto replacement;
            await using (var actDb = _fixture.CreateDbContext())
            {
                var service = CreateService(actDb, clock);
                replacement = await service.ReplaceAsync(creator.Id, original.Id, 4m, RoleType.QcManager);
                ruleIds.Add(replacement.Id);
            }

            await using var assertDb = _fixture.CreateDbContext();
            var oldRow = await assertDb.EscalationRules.AsNoTracking().SingleAsync(r => r.Id == original.Id);
            var newRow = await assertDb.EscalationRules.AsNoTracking().SingleAsync(r => r.Id == replacement.Id);

            Assert.False(oldRow.IsActive);
            Assert.Equal(t1.UtcDateTime, oldRow.RetiredAt);
            Assert.Equal(creator.Id, oldRow.RetiredByUserId);

            Assert.True(newRow.IsActive);
            Assert.Equal(original.Id, newRow.ReplacesRuleId);
            Assert.Equal(4m, newRow.ThresholdWorkingHours);
            Assert.Equal(RoleType.QcManager, newRow.NotifyRoleType);
            Assert.Equal(EscalationTrigger.ReviewWaiting, newRow.Trigger);

            var queryService = CreateService(assertDb, clock);
            var activeOnly = await queryService.ListAsync(creator.Id, section.Id, includeRetired: false);
            var withRetired = await queryService.ListAsync(creator.Id, section.Id, includeRetired: true);

            Assert.Single(activeOnly);
            Assert.Equal(replacement.Id, activeOnly[0].Id);
            Assert.Equal(2, withRetired.Count);
            Assert.Contains(withRetired, r => r.Id == original.Id && !r.IsActive);
            Assert.Contains(withRetired, r => r.Id == replacement.Id && r.IsActive && r.ReplacesRuleId == original.Id);

            var oldAudits = await assertDb.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(EscalationRule) && a.EntityId == original.Id.ToString())
                .Select(a => a.Action)
                .ToListAsync();
            Assert.Contains("Create", oldAudits);
            Assert.Contains("Update", oldAudits);

            var newAudit = await assertDb.AuditLogs.AsNoTracking()
                .SingleAsync(a => a.EntityName == nameof(EscalationRule) && a.EntityId == replacement.Id.ToString());
            Assert.Equal("Create", newAudit.Action);
            Assert.Equal(creator.Id, newAudit.UserId);
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Retire_SetsRetiredMetadata_AndAudited()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("RET");
        var actor = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var clock = new MutableTimeProvider(new DateTimeOffset(CairoUtc(2026, 11, 15, 10, 0), TimeSpan.Zero));
        var ruleIds = new List<int>();

        try
        {
            EscalationRuleDto rule;
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                rule = await CreateService(arrangeDb, clock)
                    .CreateAsync(actor.Id, section.Id, EscalationTrigger.ReviewWaiting, 8m, RoleType.SectionHead);
                ruleIds.Add(rule.Id);
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 11, 30), TimeSpan.Zero);
            await using (var actDb = _fixture.CreateDbContext())
            {
                await CreateService(actDb, clock).RetireAsync(actor.Id, rule.Id);
            }

            await using var assertDb = _fixture.CreateDbContext();
            var stored = await assertDb.EscalationRules.AsNoTracking().SingleAsync(r => r.Id == rule.Id);
            Assert.False(stored.IsActive);
            Assert.Equal(actor.Id, stored.RetiredByUserId);
            Assert.Equal(clock.Now.UtcDateTime, stored.RetiredAt);

            var actions = await assertDb.AuditLogs.AsNoTracking()
                .Where(a => a.EntityName == nameof(EscalationRule) && a.EntityId == rule.Id.ToString())
                .OrderBy(a => a.Id)
                .ToListAsync();
            Assert.Contains(actions, a => a.Action == "Update" && a.UserId == actor.Id);
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Evaluate_FiresAtThresholdInWorkingHours_NotBefore()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("THRWK");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        // Thursday 2026-11-12 15:00 Cairo (1 hour left in Thursday working day).
        // Friday 2026-11-13 and Saturday 2026-11-14 are weekend.
        // Sunday 2026-11-15 08:00..15:00 = 7 working hours => total 8 hours reached at Sunday 15:00 Cairo.
        var submittedUtc = CairoUtc(2026, 11, 12, 15, 0);
        var fridayUtc = CairoUtc(2026, 11, 13, 20, 0);
        var sundayBeforeUtc = CairoUtc(2026, 11, 15, 14, 59);
        var sundayAtThresholdUtc = CairoUtc(2026, 11, 15, 15, 0);

        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 8m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-WK-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc.AddDays(-1),
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;
            }

            clock.Now = new DateTimeOffset(fridayUtc, TimeSpan.Zero);
            await using (var friDb = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(friDb, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(sundayBeforeUtc, TimeSpan.Zero);
            await using (var beforeDb = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(beforeDb, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(sundayAtThresholdUtc, TimeSpan.Zero);
            await using (var atDb = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(atDb, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            var ev = await assertDb.EscalationEvents
                .AsNoTracking()
                .SingleAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "Sample" && e.RecordId == sampleId);
            Assert.False(ev.NoRecipient);
            Assert.Contains(head.Id, JsonSerializer.Deserialize<List<int>>(ev.RecipientUserIds)!);

            var notif = await assertDb.NotificationLogs
                .AsNoTracking()
                .SingleAsync(n => n.UserId == head.Id && n.DedupeKey == $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}");
            Assert.Equal("Escalation", notif.Type);
            Assert.Equal(section.Id, notif.SectionId);
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    // The event and its notification are one save. A publish that fails must leave no event behind;
    // otherwise later passes would skip the record and the escalation would be lost for good.
    private sealed class FailingPublisher(INotificationPublisher inner) : INotificationPublisher
    {
        public Task PublishAsync(NotificationRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("Publishing failed (test).");
        public Task ResolveAsync(string dedupeKey, CancellationToken ct = default) => inner.ResolveAsync(dedupeKey, ct);
    }

    [PostgresFact]
    public async Task Evaluate_PublishFails_NoEventRecorded_NextPassFires()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("ATOM");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 16, 10, 0); // 10 working hours > 4
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 4m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-AT-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc.AddDays(-1),
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC", SectionId = section.Id, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id, Status = SectionSignoffStatus.UnderReview, SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);
            await using (var failDb = _fixture.CreateDbContext())
            {
                var config = BuildTestConfig();
                var labClock = new LabClock(clock, CairoZone);
                var failing = new FailingPublisher(new NotificationPublisher(failDb, clock, config, labClock));
                var service = new EscalationService(failDb, failing, new NotificationRecipients(failDb, new SegregationOfDutiesGuard(failDb)),
                    new PermissionService(failDb), new UserSectionScopeService(failDb), labClock, clock);
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.EvaluateAsync());
            }

            await using (var midDb = _fixture.CreateDbContext())
                Assert.False(await midDb.EscalationEvents.AsNoTracking().AnyAsync(e => e.EscalationRuleId == ruleIds[0]));

            await using (var okDb = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(okDb, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.Single(await assertDb.EscalationEvents.AsNoTracking()
                .Where(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "Sample" && e.RecordId == sampleId).ToListAsync());
            Assert.True(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.UserId == head.Id && n.DedupeKey == $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}"));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Evaluate_FiresOncePerRecord_EvenAcrossPasses_AndConcurrentPasses()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("CONC");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var qc = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 16, 10, 0); // 10 working hours > 4
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 4m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-CC-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc.AddHours(-2),
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);

            // Race two independent DbContexts / EscalationServices concurrently via Task.WhenAll.
            var firedCounts = await Task.WhenAll(
                Task.Run(async () =>
                {
                    await using var db1 = _fixture.CreateDbContext();
                    return await CreateService(db1, clock).EvaluateAsync();
                }),
                Task.Run(async () =>
                {
                    await using var db2 = _fixture.CreateDbContext();
                    return await CreateService(db2, clock).EvaluateAsync();
                }));

            Assert.Equal(1, firedCounts.Sum());

            // Sequential third pass must also fire 0.
            await using (var pass3Db = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(pass3Db, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            var events = await assertDb.EscalationEvents
                .AsNoTracking()
                .Where(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "Sample" && e.RecordId == sampleId)
                .ToListAsync();
            Assert.Single(events);

            var dedupeKey = $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}";
            var notifications = await assertDb.NotificationLogs
                .AsNoTracking()
                .Where(n => n.DedupeKey == dedupeKey)
                .OrderBy(n => n.UserId)
                .ToListAsync();
            Assert.Equal(2, notifications.Count);
            Assert.Equal(new[] { Math.Min(head.Id, qc.Id), Math.Max(head.Id, qc.Id) }, notifications.Select(n => n.UserId).ToArray());
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Evaluate_OtherSectionRecord_NotFired()
    {
        await EnsureDefaultWorkingHoursAsync();
        var secA = await SeedSectionAsync("SECA");
        var secB = await SeedSectionAsync("SECB");
        var headA = await SeedUserInSectionAsync(RoleType.SectionHead, secA.Id);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 16, 12, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleBId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(headA.Id, secA.Id, EscalationTrigger.ReviewWaiting, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sampleB = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-OB-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = secB.Id
                };
                sampleB.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = secB.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sampleB.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = secB.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sampleB);
                await arrangeDb.SaveChangesAsync();
                sampleBId = sampleB.Id;
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);
            await using (var actDb = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(actDb, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.False(await assertDb.EscalationEvents.AsNoTracking()
                .AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordId == sampleBId));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task RetiredRule_DoesNotFire()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("RETEV");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 16, 12, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var svc = CreateService(arrangeDb, clock);
                var r = await svc.CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);
                await svc.RetireAsync(head.Id, r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-RT-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);
            await using (var actDb = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(actDb, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.False(await assertDb.EscalationEvents.AsNoTracking().AnyAsync(e => e.EscalationRuleId == ruleIds[0]));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task ReplacedRule_NewThresholdApplies_RecordsAlreadyEscalatedByOldRuleFireAgainOnlyUnderNewRule()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("REPEV");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var qc = await SeedUserInSectionAsync(RoleType.QcManager, section.Id);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var after2hUtc = CairoUtc(2026, 11, 15, 10, 0);
        var after4hUtc = CairoUtc(2026, 11, 15, 12, 0);
        var after6hUtc = CairoUtc(2026, 11, 15, 14, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r1 = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 2m, RoleType.SectionHead);
                ruleIds.Add(r1.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-RP-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;
            }

            // Old rule fires at 2 working hours.
            clock.Now = new DateTimeOffset(after2hUtc, TimeSpan.Zero);
            await using (var pass1Db = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(pass1Db, clock).EvaluateAsync());

            // Replace with new rule: 6 working hours -> QcManager.
            EscalationRuleDto r2;
            await using (var repDb = _fixture.CreateDbContext())
            {
                r2 = await CreateService(repDb, clock).ReplaceAsync(head.Id, ruleIds[0], 6m, RoleType.QcManager);
                ruleIds.Add(r2.Id);
            }

            // At 4 working hours, new rule has not reached 6h threshold yet -> 0 fired.
            clock.Now = new DateTimeOffset(after4hUtc, TimeSpan.Zero);
            await using (var pass2Db = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(pass2Db, clock).EvaluateAsync());

            // At 6 working hours, new rule fires once for QcManager (separate unique-index rule id).
            clock.Now = new DateTimeOffset(after6hUtc, TimeSpan.Zero);
            await using (var pass3Db = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(pass3Db, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.True(await assertDb.EscalationEvents.AsNoTracking().AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordId == sampleId));
            Assert.True(await assertDb.EscalationEvents.AsNoTracking().AnyAsync(e => e.EscalationRuleId == r2.Id && e.RecordId == sampleId));
            Assert.True(await assertDb.NotificationLogs.AsNoTracking().AnyAsync(n => n.UserId == qc.Id && n.DedupeKey == $"Escalation:rule:{r2.Id}:Sample:{sampleId}"));
            // Old notification stays open because sample is still UnderReview.
            var oldNotif = await assertDb.NotificationLogs.AsNoTracking()
                .SingleAsync(n => n.UserId == head.Id && n.DedupeKey == $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}");
            Assert.Null(oldNotif.ResolvedAt);
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task ReviewCompleted_ResolvesEscalationNotification()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("RES");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 15, 11, 0);
        var completedUtc = CairoUtc(2026, 11, 15, 11, 30);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-RS-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc.AddHours(-1),
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);
            await using (var fireDb = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(fireDb, clock).EvaluateAsync());

            // Transition sample section out of UnderReview (review completed).
            await using (var updateDb = _fixture.CreateDbContext())
            {
                var signoff = await updateDb.SampleSectionSignoffs.SingleAsync(s => s.SampleId == sampleId && s.SectionId == section.Id);
                signoff.Status = SectionSignoffStatus.UnderApproval;
                signoff.ReviewedAt = completedUtc;
                var sample = await updateDb.Samples.SingleAsync(s => s.Id == sampleId);
                sample.Status = SampleStatus.UnderApproval;
                sample.ReviewedAt = completedUtc;
                await updateDb.SaveChangesAsync();
            }

            clock.Now = new DateTimeOffset(completedUtc.AddMinutes(5), TimeSpan.Zero);
            await using (var resolveDb = _fixture.CreateDbContext())
                await CreateService(resolveDb, clock).EvaluateAsync();

            await using var assertDb = _fixture.CreateDbContext();
            var notif = await assertDb.NotificationLogs
                .AsNoTracking()
                .SingleAsync(n => n.UserId == head.Id && n.DedupeKey == $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}");
            Assert.NotNull(notif.ResolvedAt);
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task NoMemberWithRole_EventMarkedNoRecipient_AdminsNotified()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("NOREC");
        var creatorHead = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        // SysAdmin (not a member of `section`) holds Users.Manage.
        var sysAdmin = await SeedUserInSectionAsync(RoleType.SystemAdministrator, null, $"{UniqueCode("admin")}@example.test");
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 15, 12, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                // Target role QcManager has NO member in `section`.
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(creatorHead.Id, section.Id, EscalationTrigger.ReviewWaiting, 2m, RoleType.QcManager);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-NR-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);
            await using (var actDb = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(actDb, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            var ev = await assertDb.EscalationEvents
                .AsNoTracking()
                .SingleAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordId == sampleId);
            Assert.True(ev.NoRecipient);
            Assert.Contains(sysAdmin.Id, JsonSerializer.Deserialize<List<int>>(ev.RecipientUserIds)!);

            var adminNotif = await assertDb.NotificationLogs
                .AsNoTracking()
                .SingleAsync(n => n.UserId == sysAdmin.Id && n.DedupeKey == $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}");
            Assert.Equal("Escalation", adminNotif.Type);
            Assert.False(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.UserId == creatorHead.Id && n.DedupeKey == $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}"));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Escalation_EmailsRecipients_AndChangesNoSampleField()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("EMAIL");
        var emailAddress = $"{UniqueCode("headmail")}@lab.example";
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id, emailAddress);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 15, 11, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            Sample beforeSample;
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-EM-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc.AddHours(-1),
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;

                beforeSample = await arrangeDb.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);
            await using (var actDb = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(actDb, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            var afterSample = await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);

            Assert.Equal(beforeSample.Version, afterSample.Version);
            Assert.Equal(beforeSample.Status, afterSample.Status);
            Assert.Equal(beforeSample.ReferenceNumber, afterSample.ReferenceNumber);
            Assert.Equal(beforeSample.ReviewedAt, afterSample.ReviewedAt);
            Assert.Equal(beforeSample.ReviewedByUserId, afterSample.ReviewedByUserId);
            Assert.Equal(beforeSample.ApprovedAt, afterSample.ApprovedAt);
            Assert.Equal(beforeSample.ApprovedByUserId, afterSample.ApprovedByUserId);
            Assert.Equal(beforeSample.ReceivedAt, afterSample.ReceivedAt);

            var outbox = await assertDb.EmailOutbox
                .AsNoTracking()
                .SingleAsync(o => o.UserId == head.Id && o.ToAddress == emailAddress);
            Assert.Equal(EmailOutboxKind.Immediate, outbox.Kind);
            Assert.Contains("Escalation", outbox.Subject, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Trigger_ReviewWaiting_UsesSubmittedForReviewTime()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("TRREV");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        // Received 5 days earlier, submitted for review Sunday 10:00 -> 4h threshold reached at Sunday 14:00 (not 13:59).
        var receivedUtc = CairoUtc(2026, 11, 10, 9, 0);
        var submittedUtc = CairoUtc(2026, 11, 15, 10, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 4m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-RV-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = receivedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                // Intentionally leave SampleSectionSignoff.SubmittedForReviewAt null to prove ReviewEventLog fallback works too.
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();

                arrangeDb.ReviewWorkflowEvents.Add(new ReviewWorkflowEvent
                {
                    EntityType = ReviewEntityTypes.Sample,
                    EntityId = sample.Id,
                    SectionId = section.Id,
                    EventType = ReviewWorkflowEventType.SubmittedForReview,
                    PerformedByUserId = _fixture.SeededUserId,
                    PerformedByNameSnapshot = "Seeded",
                    Timestamp = submittedUtc
                });
                await arrangeDb.SaveChangesAsync();
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 13, 59), TimeSpan.Zero);
            await using (var dbBefore = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(dbBefore, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 14, 0), TimeSpan.Zero);
            await using (var dbAt = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(dbAt, clock).EvaluateAsync());
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Trigger_ApprovalWaiting_UsesReviewedTime()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("TRAPP");
        var qc = await SeedUserInSectionAsync(RoleType.QcManager, section.Id);
        var headCreator = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        // Submitted at 08:00, reviewed at 11:00; 3h threshold must count from 11:00 -> fires at 14:00, NOT 13:59.
        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var reviewedUtc = CairoUtc(2026, 11, 15, 11, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(reviewedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(headCreator.Id, section.Id, EscalationTrigger.ApprovalWaiting, 3m, RoleType.QcManager);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-AP-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderApproval,
                    ReceivedAt = submittedUtc.AddDays(-1),
                    ReceivedByUserId = _fixture.SeededUserId,
                    ReviewedAt = reviewedUtc,
                    ReviewedByUserId = headCreator.Id,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Reviewed
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderApproval,
                    SubmittedForReviewAt = submittedUtc,
                    ReviewedAt = reviewedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 13, 59), TimeSpan.Zero);
            await using (var dbBefore = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(dbBefore, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 14, 0), TimeSpan.Zero);
            await using (var dbAt = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(dbAt, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.True(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.UserId == qc.Id && n.DedupeKey!.StartsWith($"Escalation:rule:{ruleIds[0]}:Sample:")));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Trigger_ReturnedNotCorrected_UsesReturnedAt()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("TRRET");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var analyst = await SeedUserInSectionAsync(RoleType.Analyst, section.Id);
        var cause = await EnsureCauseAsync();

        // Received earlier, returned at Sunday 11:00; 2h threshold must fire at Sunday 13:00, not 12:59.
        var receivedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var returnedUtc = CairoUtc(2026, 11, 15, 11, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(returnedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int orderId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReturnedNotCorrected, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-RN-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.InTesting,
                    ReceivedAt = receivedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                var order = new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.InProgress,
                    CurrentStep = WorkflowStep.Running,
                    AssignedAnalystId = analyst.Id
                };
                sample.TestOrders.Add(order);
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                orderId = order.Id;

                arrangeDb.TestReturnEvents.Add(new TestReturnEvent
                {
                    TestOrderId = order.Id,
                    ReviewerUserId = head.Id,
                    AssignedAnalystId = analyst.Id,
                    Reason = "Plate recount required",
                    ReturnedAt = returnedUtc
                });
                await arrangeDb.SaveChangesAsync();
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 12, 59), TimeSpan.Zero);
            await using (var dbBefore = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(dbBefore, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 13, 0), TimeSpan.Zero);
            await using (var dbAt = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(dbAt, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.True(await assertDb.EscalationEvents.AsNoTracking()
                .AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "TestOrder" && e.RecordId == orderId));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Trigger_IncubationNotRead_UsesReadyTime()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("TRINC");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var analyst = await SeedUserInSectionAsync(RoleType.Analyst, section.Id);
        var cause = await EnsureCauseAsync();

        // Incubation started 08:00, ready at 12:00; 2h threshold must fire at 14:00, NOT 13:59.
        var startedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var readyUtc = CairoUtc(2026, 11, 15, 12, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(readyUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int incubationId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.IncubationNotRead, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-IN-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.InTesting,
                    ReceivedAt = startedUtc.AddHours(-1),
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                var order = new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.InProgress,
                    CurrentStep = WorkflowStep.Incubating,
                    AssignedAnalystId = analyst.Id
                };
                sample.TestOrders.Add(order);
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();

                var incubation = new Incubation
                {
                    TestOrderId = order.Id,
                    StepNumber = 1,
                    StepName = "Stage 1",
                    StageNumber = 1,
                    StartedAt = startedUtc,
                    ExpectedReadingAt = readyUtc,
                    StartedByUserId = analyst.Id
                };
                arrangeDb.Incubations.Add(incubation);
                await arrangeDb.SaveChangesAsync();
                incubationId = incubation.Id;
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 13, 59), TimeSpan.Zero);
            await using (var dbBefore = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(dbBefore, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 14, 0), TimeSpan.Zero);
            await using (var dbAt = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(dbAt, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.True(await assertDb.EscalationEvents.AsNoTracking()
                .AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "Incubation" && e.RecordId == incubationId));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Trigger_TestingNotStarted_UsesReceivedAt_WhenNoStepRecorded()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("TRNS");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var analyst = await SeedUserInSectionAsync(RoleType.Analyst, section.Id);
        var cause = await EnsureCauseAsync();

        var receivedUtc = CairoUtc(2026, 11, 15, 9, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(receivedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int unstartedSampleId;
        int startedSampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.TestingNotStarted, 3m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var unstarted = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-NS1-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.Received,
                    ReceivedAt = receivedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                unstarted.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Waiting
                });

                var started = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-NS2-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.InTesting,
                    ReceivedAt = receivedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                var startedOrder = new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.InProgress,
                    CurrentStep = WorkflowStep.Running,
                    AssignedAnalystId = analyst.Id
                };
                started.TestOrders.Add(startedOrder);

                arrangeDb.Samples.AddRange(unstarted, started);
                await arrangeDb.SaveChangesAsync();
                unstartedSampleId = unstarted.Id;
                startedSampleId = started.Id;
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 11, 59), TimeSpan.Zero);
            await using (var dbBefore = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(dbBefore, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 12, 0), TimeSpan.Zero);
            await using (var dbAt = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(dbAt, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.True(await assertDb.EscalationEvents.AsNoTracking()
                .AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "Sample" && e.RecordId == unstartedSampleId));
            Assert.False(await assertDb.EscalationEvents.AsNoTracking()
                .AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordId == startedSampleId));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Trigger_DocumentTaskOverdue_UsesDueDate()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("TRDOC");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var reviewer = await SeedUserInSectionAsync(RoleType.Reviewer, section.Id);

        // Assigned 08:00, due date 11:00; 2h rule fires at 13:00, NOT 12:59.
        var assignedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var dueUtc = CairoUtc(2026, 11, 15, 11, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(dueUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int taskId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.DocumentTaskOverdue, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var master = new DocumentMaster
                {
                    MicroLimsDocumentId = UniqueCode("DOC-ESC-"),
                    CompanyDocumentCode = UniqueCode("SOP-ESC-"),
                    Title = "Escalation Document Task Test",
                    DocumentTypeId = _fixture.SeededDocTypeId,
                    DepartmentId = _fixture.SeededDepartmentId,
                    SectionId = section.Id,
                    DocumentOwnerUserId = head.Id,
                    CreatedByUserId = head.Id,
                    RecordStatus = DocumentRecordStatus.Active
                };
                arrangeDb.DocumentMasters.Add(master);
                await arrangeDb.SaveChangesAsync();

                var rev = new DocumentRevision
                {
                    DocumentMasterId = master.Id,
                    RevisionNumber = "01",
                    RevisionSequence = 1,
                    RevisionStatus = DocumentRevisionStatus.InReview,
                    CreatedByUserId = head.Id,
                    CreatedAt = assignedUtc
                };
                arrangeDb.DocumentRevisions.Add(rev);
                await arrangeDb.SaveChangesAsync();

                var reviewTask = new DocumentReviewTask
                {
                    DocumentRevisionId = rev.Id,
                    AssignedReviewerUserId = reviewer.Id,
                    AssignedByUserId = head.Id,
                    AssignedAt = assignedUtc,
                    DueDate = dueUtc,
                    Status = ReviewTaskStatus.Pending
                };
                arrangeDb.DocumentReviewTasks.Add(reviewTask);
                await arrangeDb.SaveChangesAsync();
                taskId = reviewTask.Id;
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 12, 59), TimeSpan.Zero);
            await using (var dbBefore = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(dbBefore, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 15, 13, 0), TimeSpan.Zero);
            await using (var dbAt = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(dbAt, clock).EvaluateAsync());

            await using var assertDb = _fixture.CreateDbContext();
            Assert.True(await assertDb.EscalationEvents.AsNoTracking()
                .AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "DocumentReviewTask" && e.RecordId == taskId));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    [PostgresFact]
    public async Task Evaluate_SkipsLabCalendarNonWorkingDate()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("CAL");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        // Sunday 2026-11-15 submitted at 14:00 (2h left on Sunday). Monday 2026-11-16 marked holiday -> skipped!
        // Tuesday 2026-11-17 08:00..10:00 supplies remaining 2h => 4h reached Tuesday 10:00, not Monday 12:00.
        var submittedUtc = CairoUtc(2026, 11, 15, 14, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        var dateIds = new List<int>();

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 4m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var calDate = new LabCalendarDate
                {
                    Date = new DateOnly(2026, 11, 16),
                    Description = "Test Holiday"
                };
                arrangeDb.LabCalendarDates.Add(calDate);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-CL-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc.AddHours(-1),
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                dateIds.Add(calDate.Id);
            }

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 16, 12, 0), TimeSpan.Zero);
            await using (var monDb = _fixture.CreateDbContext())
                Assert.Equal(0, await CreateService(monDb, clock).EvaluateAsync());

            clock.Now = new DateTimeOffset(CairoUtc(2026, 11, 17, 10, 0), TimeSpan.Zero);
            await using (var tueDb = _fixture.CreateDbContext())
                Assert.Equal(1, await CreateService(tueDb, clock).EvaluateAsync());
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds, dateIds);
        }
    }

    [PostgresFact]
    public async Task NotificationScheduler_RunAsync_EvaluatesEscalationRules()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("SCHED");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var cause = await EnsureCauseAsync();

        var submittedUtc = CairoUtc(2026, 11, 15, 8, 0);
        var evalUtc = CairoUtc(2026, 11, 15, 11, 0);
        var clock = new MutableTimeProvider(new DateTimeOffset(submittedUtc, TimeSpan.Zero));
        var ruleIds = new List<int>();
        int sampleId;

        try
        {
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                var r = await CreateService(arrangeDb, clock)
                    .CreateAsync(head.Id, section.Id, EscalationTrigger.ReviewWaiting, 2m, RoleType.SectionHead);
                ruleIds.Add(r.Id);

                var sample = new Sample
                {
                    ReferenceNumber = UniqueCode("ESC-SC-"),
                    Category = SampleCategory.FinishedProduct,
                    Status = SampleStatus.UnderReview,
                    ReceivedAt = submittedUtc,
                    ReceivedByUserId = _fixture.SeededUserId,
                    CauseOfTestingId = cause.Id,
                    LabSectionId = section.Id
                };
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = "TAMC",
                    SectionId = section.Id,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Ready
                });
                sample.SectionSignoffs.Add(new SampleSectionSignoff
                {
                    SectionId = section.Id,
                    Status = SectionSignoffStatus.UnderReview,
                    SubmittedForReviewAt = submittedUtc
                });
                arrangeDb.Samples.Add(sample);
                await arrangeDb.SaveChangesAsync();
                sampleId = sample.Id;
            }

            clock.Now = new DateTimeOffset(evalUtc, TimeSpan.Zero);
            await using (var actDb = _fixture.CreateDbContext())
            {
                var scheduler = TestServiceFactory.NotificationScheduler(actDb, clock, config: BuildTestConfig());
                var count = await scheduler.RunAsync();
                Assert.True(count >= 1);
            }

            await using var assertDb = _fixture.CreateDbContext();
            Assert.True(await assertDb.EscalationEvents.AsNoTracking()
                .AnyAsync(e => e.EscalationRuleId == ruleIds[0] && e.RecordType == "Sample" && e.RecordId == sampleId));
            Assert.True(await assertDb.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.UserId == head.Id && n.DedupeKey == $"Escalation:rule:{ruleIds[0]}:Sample:{sampleId}"));
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }

    private const string TestPassword = "Esc-Pass-123!";
    private static readonly string FixedJwtKeyBase64 =
        Convert.ToBase64String(Enumerable.Range(1, 48).Select(i => (byte)i).ToArray());

    private async Task<(WebApplication App, HttpClient Client)> StartTestServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = "Testing";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = FixedJwtKeyBase64,
            ["Jwt:Issuer"] = "MicroLIMS",
            ["Jwt:Audience"] = "MicroLIMS.Client",
            ["Mfa:ActiveKeyId"] = "test-k1",
            ["Mfa:EncryptionKeys:test-k1"] = FixedJwtKeyBase64,
            ["Lab:TimeZoneId"] = "Africa/Cairo",
            ["ConnectionStrings:DefaultConnection"] = _fixture.TestConnectionString
        });

        var jwtSettings = JwtConfiguration.Resolve(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(jwtSettings);
        builder.Services.AddMicroLimsDbContext(opt => opt.UseNpgsql(_fixture.TestConnectionString));
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddMicroLimsJwtAuthentication(jwtSettings);
        builder.Services.AddMicroLimsAuthorization();
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);
        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();
                options.Filters.Add<MustChangePasswordFilter>();
            })
            .AddApplicationPart(typeof(EscalationRulesController).Assembly)
            .AddJsonOptions(o => ApiJsonOptions.Configure(o.JsonSerializerOptions));

        var app = builder.Build();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { username, password = TestPassword });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("token").GetString()!;
    }

    [PostgresFact]
    public async Task EscalationRulesController_EnforcesPermissionsAndCrud()
    {
        await EnsureDefaultWorkingHoursAsync();
        var section = await SeedSectionAsync("HTTP");
        var head = await SeedUserInSectionAsync(RoleType.SectionHead, section.Id);
        var analyst = await SeedUserInSectionAsync(RoleType.Analyst, section.Id);
        var ruleIds = new List<int>();

        var (app, client) = await StartTestServerAsync();
        await using var _ = app;

        try
        {
            var analystToken = await LoginAsync(client, analyst.Username);
            using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/escalation-rules?sectionId={section.Id}"))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", analystToken);
                var forbidden = await client.SendAsync(req);
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            }

            var headToken = await LoginAsync(client, head.Username);

            int createdId;
            using (var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/escalation-rules"))
            {
                createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headToken);
                createReq.Content = JsonContent.Create(new
                {
                    sectionId = section.Id,
                    trigger = EscalationTrigger.ReviewWaiting,
                    thresholdWorkingHours = 8m,
                    notifyRoleType = RoleType.SectionHead
                });
                var createResp = await client.SendAsync(createReq);
                Assert.Equal(HttpStatusCode.OK, createResp.StatusCode);
                using var createdJson = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
                createdId = createdJson.RootElement.GetProperty("data").GetProperty("id").GetInt32();
                ruleIds.Add(createdId);
            }

            using (var listReq = new HttpRequestMessage(HttpMethod.Get, $"/api/escalation-rules?sectionId={section.Id}&includeRetired=false"))
            {
                listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headToken);
                var listResp = await client.SendAsync(listReq);
                Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
                using var listJson = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
                Assert.Single(listJson.RootElement.GetProperty("data").EnumerateArray());
            }

            int replacedId;
            using (var replaceReq = new HttpRequestMessage(HttpMethod.Post, $"/api/escalation-rules/{createdId}/replace"))
            {
                replaceReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headToken);
                replaceReq.Content = JsonContent.Create(new
                {
                    thresholdWorkingHours = 4m,
                    notifyRoleType = RoleType.QcManager
                });
                var replaceResp = await client.SendAsync(replaceReq);
                Assert.Equal(HttpStatusCode.OK, replaceResp.StatusCode);
                using var replacedJson = JsonDocument.Parse(await replaceResp.Content.ReadAsStringAsync());
                replacedId = replacedJson.RootElement.GetProperty("data").GetProperty("id").GetInt32();
                ruleIds.Add(replacedId);
            }

            using (var retireReq = new HttpRequestMessage(HttpMethod.Post, $"/api/escalation-rules/{replacedId}/retire"))
            {
                retireReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", headToken);
                var retireResp = await client.SendAsync(retireReq);
                Assert.Equal(HttpStatusCode.OK, retireResp.StatusCode);
            }
        }
        finally
        {
            await CleanupRulesAndDatesAsync(ruleIds);
        }
    }
}
