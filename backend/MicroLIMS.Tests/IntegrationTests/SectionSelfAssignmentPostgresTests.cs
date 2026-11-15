using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.API.Controllers;
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
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Per-section self-assignment setting (Section Head ownership, mandatory reason, GxP audit trail,
// optimistic concurrency, Take/MyWork gating, Release preserved, and migration carry-over).
// Every test creates its own isolated sections and uses separate Arrange / Act / Assert DbContexts.
[Collection("PostgresDatabaseCollection")]
public class SectionSelfAssignmentPostgresTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 10, 30, 0, DateTimeKind.Utc);
    private const string RefusalMessage = "Taking tests is switched off in this section. Ask a Section Head to assign this test.";

    private readonly PostgresTestFixture _fixture;

    public SectionSelfAssignmentPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class StubSettings : ISystemSettingsReader
    {
        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == SystemSettingKeys.MyWorkLookAheadDays ? 7 : 0);
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }

    private sealed class NoopErrors : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static SectionSelfAssignmentService BuildSettingService(MicroLimsDbContext db, DateTime? at = null) =>
        new(db, new UserSectionScopeService(db), new FixedTimeProvider(new DateTimeOffset(at ?? Now)));

    private static TestSelfAssignmentService BuildTakeService(MicroLimsDbContext db, DateTime? at = null) =>
        new(db, new UserSectionScopeService(db), new FixedTimeProvider(new DateTimeOffset(at ?? Now)));

    private static MyWorkService BuildMyWork(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db),
            new LabClock(new FixedTimeProvider(new DateTimeOffset(Now)), LabClock.ResolveTimeZone("Africa/Cairo")),
            new StubSettings(), new NoopErrors(),
            new IMyWorkSource[] { new AssignedTestsSource(db), new AvailableTestsSource(db), new UnassignedTestsSource(db) });

    private static async Task<int> CauseIdAsync(MicroLimsDbContext db)
    {
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is not null) return cause.Id;
        cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        return cause.Id;
    }

    private static async Task<DocumentSection> NewSectionAsync(MicroLimsDbContext db, string label)
    {
        var template = await db.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "MICRO");
        var uid = Guid.NewGuid().ToString("N")[..8];
        var section = new DocumentSection { DepartmentId = template.DepartmentId, Name = $"{label} {uid}", Code = $"SA{uid[..6]}", IsActive = true };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        return section;
    }

    private static async Task<User> NewUserAsync(MicroLimsDbContext db, string label, RoleType roleType, params DocumentSection[] memberOf)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var role = await db.Roles.AsNoTracking().Where(r => r.Type == roleType).OrderBy(r => r.Id).FirstAsync();
        var user = new User { Username = $"{label}_{uid}", FullName = $"{label} {uid}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        foreach (var section in memberOf)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<TestOrder> NewTestAsync(MicroLimsDbContext db, int causeId, DocumentSection section)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var receiver = await db.Users.AsNoTracking().OrderBy(u => u.Id).FirstAsync();
        var sample = new Sample
        {
            ReferenceNumber = $"SSA-{uid}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = causeId,
            ReceivedAt = Now.AddHours(-2),
            ReceivedByUserId = receiver.Id,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = $"T{uid}",
            SectionId = section.Id,
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private async Task<List<AuditLog>> SettingAuditsAsync(int sectionId)
    {
        await using var assertDb = _fixture.CreateDbContext();
        return await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(SectionSelfAssignmentSetting) && a.EntityId == sectionId.ToString())
            .OrderBy(a => a.Id)
            .ToListAsync();
    }

    [PostgresFact]
    public async Task DefaultState_IsEnabled_WhenNoRowExists()
    {
        DocumentSection section;
        User head;
        await using (var arrange = _fixture.CreateDbContext())
        {
            section = await NewSectionAsync(arrange, "DefaultOn");
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, section);
        }

        IReadOnlyList<SectionSelfAssignmentDto> list;
        bool isEnabled;
        List<int> disabledIds;
        await using (var act = _fixture.CreateDbContext())
        {
            var svc = BuildSettingService(act);
            list = await svc.GetForUserAsync(head.Id);
            isEnabled = await svc.IsEnabledAsync(section.Id);
            disabledIds = await svc.DisabledSectionIds().Where(id => id == section.Id).ToListAsync();
        }

        var item = Assert.Single(list, x => x.SectionId == section.Id);
        Assert.Equal(section.Name, item.SectionName);
        Assert.True(item.Enabled);
        Assert.Null(item.ChangedAt);
        Assert.Null(item.ChangedByName);
        Assert.Equal(0u, item.Version);
        Assert.True(isEnabled);
        Assert.Empty(disabledIds);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SectionSelfAssignmentSettings.AsNoTracking().AnyAsync(s => s.SectionId == section.Id));
    }

    [PostgresFact]
    public async Task SectionHead_DisablesThenEnablesWithReason_PersistsRowAndAuditsOldAndNewValues()
    {
        DocumentSection section;
        User head;
        await using (var arrange = _fixture.CreateDbContext())
        {
            section = await NewSectionAsync(arrange, "ToggleAudit");
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, section);
        }

        const string disableReason = "Control bench workload during audit week";
        SectionSelfAssignmentDto disabledDto;
        await using (var act = _fixture.CreateDbContext(new FixedTimeProvider(new DateTimeOffset(Now))))
        {
            disabledDto = await BuildSettingService(act, Now).SetAsync(head.Id, section.Id, false, $"  {disableReason}  ");
        }

        Assert.False(disabledDto.Enabled);
        Assert.Equal(Now, disabledDto.ChangedAt);
        Assert.Equal(head.FullName, disabledDto.ChangedByName);
        Assert.NotEqual(0u, disabledDto.Version);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var persisted = await assertDb.SectionSelfAssignmentSettings.AsNoTracking().SingleAsync(s => s.SectionId == section.Id);
            Assert.False(persisted.Enabled);
            Assert.Equal(head.Id, persisted.ChangedByUserId);
            Assert.Equal(Now, persisted.ChangedAt);
            Assert.Equal(disabledDto.Version, persisted.Version);
        }

        var auditsAfterDisable = await SettingAuditsAsync(section.Id);
        var createAudit = Assert.Single(auditsAfterDisable);
        Assert.Equal("Create", createAudit.Action);
        Assert.Equal(head.Id, createAudit.UserId);
        Assert.Equal(disableReason, createAudit.Reason);
        Assert.Equal(Now, createAudit.Timestamp);
        Assert.Null(createAudit.PreviousValue);
        using (var doc = JsonDocument.Parse(createAudit.NewValue!))
        {
            Assert.False(doc.RootElement.GetProperty("Enabled").GetBoolean());
            Assert.Equal(section.Id, doc.RootElement.GetProperty("SectionId").GetInt32());
        }

        var later = Now.AddHours(2);
        const string enableReason = "Workload normal; resume analyst self-assignment";
        SectionSelfAssignmentDto enabledDto;
        await using (var act = _fixture.CreateDbContext(new FixedTimeProvider(new DateTimeOffset(later))))
        {
            enabledDto = await BuildSettingService(act, later).SetAsync(head.Id, section.Id, true, enableReason, disabledDto.Version);
        }

        Assert.True(enabledDto.Enabled);
        Assert.Equal(later, enabledDto.ChangedAt);
        Assert.NotEqual(disabledDto.Version, enabledDto.Version);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var persisted = await assertDb.SectionSelfAssignmentSettings.AsNoTracking().SingleAsync(s => s.SectionId == section.Id);
            Assert.True(persisted.Enabled);
            Assert.Equal(head.Id, persisted.ChangedByUserId);
            Assert.Equal(later, persisted.ChangedAt);
        }

        var auditsAfterEnable = await SettingAuditsAsync(section.Id);
        Assert.Equal(2, auditsAfterEnable.Count);
        var updateAudit = auditsAfterEnable[1];
        Assert.Equal("Update", updateAudit.Action);
        Assert.Equal(head.Id, updateAudit.UserId);
        Assert.Equal(enableReason, updateAudit.Reason);
        Assert.Equal(later, updateAudit.Timestamp);
        using (var prevDoc = JsonDocument.Parse(updateAudit.PreviousValue!))
        using (var newDoc = JsonDocument.Parse(updateAudit.NewValue!))
        {
            Assert.False(prevDoc.RootElement.GetProperty("Enabled").GetBoolean());
            Assert.True(newDoc.RootElement.GetProperty("Enabled").GetBoolean());
        }
    }

    [PostgresFact]
    public async Task DisabledSection_RefusesTakeWithSectionMessage_AllowsTakeInEnabledSection_AndFiltersMyWork()
    {
        DocumentSection disabledSec, enabledSec;
        User head, analyst;
        TestOrder disabledOrder, enabledOrder;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var causeId = await CauseIdAsync(arrange);
            disabledSec = await NewSectionAsync(arrange, "DisabledSec");
            enabledSec = await NewSectionAsync(arrange, "EnabledSec");
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, disabledSec);
            analyst = await NewUserAsync(arrange, "analyst", RoleType.Analyst, disabledSec, enabledSec);
            disabledOrder = await NewTestAsync(arrange, causeId, disabledSec);
            enabledOrder = await NewTestAsync(arrange, causeId, enabledSec);
        }

        await using (var act = _fixture.CreateDbContext())
            await BuildSettingService(act).SetAsync(head.Id, disabledSec.Id, false, "Assign manually during staff training");

        // Take in disabled section is refused with exact message.
        await using (var act = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => BuildTakeService(act).TakeAsync(disabledOrder.Id, analyst.Id));
            Assert.Equal(RefusalMessage, ex.Message);
        }

        // Take in enabled section succeeds.
        await using (var act = _fixture.CreateDbContext())
            await BuildTakeService(act).TakeAsync(enabledOrder.Id, analyst.Id);

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Null((await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == disabledOrder.Id)).AssignedAnalystId);
            Assert.Equal(analyst.Id, (await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == enabledOrder.Id)).AssignedAnalystId);
        }

        // My Work available list excludes disabled section and includes enabled section (before enabledOrder was taken).
        TestOrder secondEnabledOrder;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var causeId = await CauseIdAsync(arrange);
            secondEnabledOrder = await NewTestAsync(arrange, causeId, enabledSec);
        }

        MyWorkResponse myWork;
        await using (var act = _fixture.CreateDbContext())
        {
            myWork = await BuildMyWork(act).GetAsync(analyst.Id, null);
            Assert.Empty(myWork.UnavailableSources);
        }

        var available = myWork.Groups.Single(g => g.Key == "available").Items;
        Assert.DoesNotContain(available, i => i.Target.TestOrderId == disabledOrder.Id);
        Assert.Contains(available, i => i.Target.TestOrderId == secondEnabledOrder.Id);
    }

    [PostgresFact]
    public async Task Release_StillWorks_WhenSectionIsDisabled()
    {
        DocumentSection section;
        User head, analyst;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var causeId = await CauseIdAsync(arrange);
            section = await NewSectionAsync(arrange, "ReleaseSec");
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, section);
            analyst = await NewUserAsync(arrange, "analyst", RoleType.Analyst, section);
            order = await NewTestAsync(arrange, causeId, section);
        }

        // Take while enabled, then switch off self-assignment.
        await using (var act = _fixture.CreateDbContext())
            await BuildTakeService(act).TakeAsync(order.Id, analyst.Id);
        await using (var act = _fixture.CreateDbContext())
            await BuildSettingService(act).SetAsync(head.Id, section.Id, false, "Switched off after analyst took test");

        // Analyst can still give the test back.
        await using (var act = _fixture.CreateDbContext())
            await BuildTakeService(act).ReleaseAsync(order.Id, analyst.Id);

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == order.Id);
        Assert.Null(persisted.AssignedAnalystId);
        var releasedAudit = await assertDb.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityName == "TestOrder" && a.EntityId == order.Id.ToString() && a.Action == "TEST_RELEASED");
        Assert.Equal(analyst.Id, releasedAudit.UserId);
    }

    [PostgresFact]
    public async Task Set_NonMember_IsRefused_WithNothingWritten()
    {
        DocumentSection section, otherSection;
        User outsider;
        await using (var arrange = _fixture.CreateDbContext())
        {
            section = await NewSectionAsync(arrange, "TargetSec");
            otherSection = await NewSectionAsync(arrange, "OtherSec");
            outsider = await NewUserAsync(arrange, "outsider", RoleType.SectionHead, otherSection);
        }

        await using (var act = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                BuildSettingService(act).SetAsync(outsider.Id, section.Id, false, "Attempt by non-member Section Head"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SectionSelfAssignmentSettings.AsNoTracking().AnyAsync(s => s.SectionId == section.Id));
        Assert.Empty(await SettingAuditsAsync(section.Id));
    }

    [PostgresFact]
    public async Task Set_InvalidReason_IsRefused_WithFieldError_AndNothingWritten()
    {
        DocumentSection section;
        User head;
        await using (var arrange = _fixture.CreateDbContext())
        {
            section = await NewSectionAsync(arrange, "ReasonSec");
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, section);
        }

        var invalidReasons = new string?[]
        {
            null,
            "",
            "         ",
            "123456789",               // 9 chars
            "   123456789   ",         // 9 chars after trim
            new string('x', 501)       // 501 chars
        };

        foreach (var bad in invalidReasons)
        {
            await using var act = _fixture.CreateDbContext();
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                BuildSettingService(act).SetAsync(head.Id, section.Id, false, bad));
            Assert.Contains(ex.FieldErrors, e => e.Field == "reason");
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.SectionSelfAssignmentSettings.AsNoTracking().AnyAsync(s => s.SectionId == section.Id));
        Assert.Empty(await SettingAuditsAsync(section.Id));
    }

    [PostgresFact]
    public async Task Set_UnchangedValue_WritesNothing()
    {
        DocumentSection section;
        User head;
        await using (var arrange = _fixture.CreateDbContext())
        {
            section = await NewSectionAsync(arrange, "NoOpSec");
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, section);
        }

        // Default is enabled: setting enabled=true writes no row and no audit.
        SectionSelfAssignmentDto noOpFromDefault;
        await using (var act = _fixture.CreateDbContext())
            noOpFromDefault = await BuildSettingService(act).SetAsync(head.Id, section.Id, true, "Already enabled by default");
        Assert.True(noOpFromDefault.Enabled);
        Assert.Equal(0u, noOpFromDefault.Version);

        await using (var assertDb = _fixture.CreateDbContext())
            Assert.False(await assertDb.SectionSelfAssignmentSettings.AsNoTracking().AnyAsync(s => s.SectionId == section.Id));
        Assert.Empty(await SettingAuditsAsync(section.Id));

        // Disable once -> 1 row, 1 audit.
        SectionSelfAssignmentDto disabled;
        await using (var act = _fixture.CreateDbContext())
            disabled = await BuildSettingService(act).SetAsync(head.Id, section.Id, false, "Disable self-assignment for this section");

        // Disable again with same value -> no-op: no extra audit, version unchanged.
        SectionSelfAssignmentDto noOpDisabled;
        await using (var act = _fixture.CreateDbContext())
            noOpDisabled = await BuildSettingService(act).SetAsync(head.Id, section.Id, false, "Repeating disabled value should do nothing", disabled.Version);

        Assert.False(noOpDisabled.Enabled);
        Assert.Equal(disabled.Version, noOpDisabled.Version);
        Assert.Single(await SettingAuditsAsync(section.Id));
    }

    [PostgresFact]
    public async Task Set_StaleVersion_IsRefused_WithConcurrencyException_AndKeepsCurrentValue()
    {
        DocumentSection section;
        User head1, head2;
        await using (var arrange = _fixture.CreateDbContext())
        {
            section = await NewSectionAsync(arrange, "StaleVerSec");
            head1 = await NewUserAsync(arrange, "head1", RoleType.SectionHead, section);
            head2 = await NewUserAsync(arrange, "head2", RoleType.SectionHead, section);
        }

        // Absent row: sending non-zero version is refused.
        await using (var act = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                BuildSettingService(act).SetAsync(head1.Id, section.Id, false, "Stale non-zero version on absent row", 99u));
        }

        SectionSelfAssignmentDto first;
        await using (var act = _fixture.CreateDbContext())
            first = await BuildSettingService(act, Now).SetAsync(head1.Id, section.Id, false, "First head disables self-assignment");

        // Second head updates using current version -> succeeds and bumps version.
        var later = Now.AddMinutes(10);
        SectionSelfAssignmentDto second;
        await using (var act = _fixture.CreateDbContext())
            second = await BuildSettingService(act, later).SetAsync(head2.Id, section.Id, true, "Second head re-enables self-assignment", first.Version);

        Assert.NotEqual(first.Version, second.Version);

        // First head tries to save using stale first.Version -> refused!
        await using (var act = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                BuildSettingService(act, Now.AddMinutes(20)).SetAsync(head1.Id, section.Id, false, "Stale write should be rejected", first.Version));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.SectionSelfAssignmentSettings.AsNoTracking().SingleAsync(s => s.SectionId == section.Id);
        Assert.True(persisted.Enabled);
        Assert.Equal(head2.Id, persisted.ChangedByUserId);
        Assert.Equal(2, (await SettingAuditsAsync(section.Id)).Count);
    }

    [Fact]
    public void Controller_IsGatedBySamplesAssignAnalyst_AndExcludedFromSystemAdmin()
    {
        var controller = typeof(SectionSelfAssignmentController);
        var auth = controller.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(PermissionConstants.SamplesAssignAnalyst, auth!.Policy);
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());

        var get = controller.GetMethod(nameof(SectionSelfAssignmentController.GetAll));
        var put = controller.GetMethod(nameof(SectionSelfAssignmentController.Update));
        Assert.NotNull(get?.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(put?.GetCustomAttribute<HttpPutAttribute>());

        Assert.DoesNotContain(PermissionConstants.SamplesAssignAnalyst, PermissionConstants.SystemAdministratorGrants);
        Assert.Contains(PermissionConstants.SamplesAssignAnalyst, SegregationOfDutiesRules.GxpExecutionAndApprovalCodes);
    }

    [PostgresFact]
    public async Task Migration_CarriesOverDisabledGlobalSetting_ToAllDocumentSections_AndLeavesDefaultWhenTrueOrAbsent()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_AddSectionSelfAssignmentSetting", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];

            await db.Database.MigrateAsync(before);
            await db.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ""DocumentDepartments"" (""Name"", ""Code"", ""IsActive"") VALUES ('Migration Dept', 'MIGD', TRUE);
                  INSERT INTO ""DocumentSections"" (""Name"", ""Code"", ""DepartmentId"", ""IsActive"")
                  SELECT 'Mig Section 1', 'MIG1', ""Id"", TRUE FROM ""DocumentDepartments"" WHERE ""Code"" = 'MIGD';
                  INSERT INTO ""DocumentSections"" (""Name"", ""Code"", ""DepartmentId"", ""IsActive"")
                  SELECT 'Mig Section 2', 'MIG2', ""Id"", TRUE FROM ""DocumentDepartments"" WHERE ""Code"" = 'MIGD';
                  INSERT INTO ""SystemSettings"" (""Key"", ""Value"", ""ValueType"", ""Category"", ""IsGxpRelevant"", ""Description"", ""CurrentVersion"")
                  VALUES ('workflow.selfAssignment.enabled', 'false', 2, 'Workflow', FALSE, 'Migrated setting', 1);");

            await db.Database.MigrateAsync(target);
            db.ChangeTracker.Clear();

            var sectionIds = await db.DocumentSections.AsNoTracking().Select(s => s.Id).OrderBy(id => id).ToListAsync();
            var rows = await db.SectionSelfAssignmentSettings.AsNoTracking().OrderBy(s => s.SectionId).ToListAsync();
            Assert.NotEmpty(sectionIds);
            Assert.Equal(sectionIds, rows.Select(r => r.SectionId).ToList());
            Assert.All(rows, r =>
            {
                Assert.False(r.Enabled);
                Assert.Null(r.ChangedByUserId);
                Assert.NotEqual(default, r.ChangedAt);
            });
        });

        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_AddSectionSelfAssignmentSetting", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];

            await db.Database.MigrateAsync(before);
            await db.Database.ExecuteSqlRawAsync(
                @"INSERT INTO ""DocumentDepartments"" (""Name"", ""Code"", ""IsActive"") VALUES ('Migration Dept', 'MIGD', TRUE);
                  INSERT INTO ""DocumentSections"" (""Name"", ""Code"", ""DepartmentId"", ""IsActive"")
                  SELECT 'Mig Section 1', 'MIG1', ""Id"", TRUE FROM ""DocumentDepartments"" WHERE ""Code"" = 'MIGD';");

            await db.Database.MigrateAsync(target);
            db.ChangeTracker.Clear();

            Assert.Empty(await db.SectionSelfAssignmentSettings.AsNoTracking().ToListAsync());
        });
    }
}
