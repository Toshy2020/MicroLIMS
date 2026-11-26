using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using static MicroLIMS.Tests.IntegrationTests.ItemSopResolverPostgresTests;

namespace MicroLIMS.Tests.IntegrationTests;

// D4 Task 6: per-laboratory switch, impact preview, qualification matrix and expiry reminders.
// Separate arrange / act / assert contexts throughout; assert queries use AsNoTracking.
[Collection("PostgresDatabaseCollection")]
public class CompetencySettingsAndMatrixPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "gate-change-password";
    private static readonly DateTimeOffset Now = new(2037, 1, 30, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly LabToday = new(2037, 1, 31);
    private readonly FixedTimeProvider _clock = new(Now);

    private LabClock Clock => new(_clock, LabClock.ResolveTimeZone("Africa/Cairo"));

    private CompetencySettingsService Settings(MicroLimsDbContext db) =>
        new(db, new PermissionService(db), new UserSectionScopeService(db), Clock,
            new ElectronicSignatureService(db, _clock), new CompetencyGuard(db, Clock, new ItemSopResolver(db)));

    private CompetencyMatrixService Matrix(MicroLimsDbContext db) =>
        new(db, new UserSectionScopeService(db), Clock, new PermissionService(db), new ItemSopResolver(db),
            new DataExportAuditService(db, _clock));

    private CompetencyReminderService Reminders(MicroLimsDbContext db) => new(db, Clock);

    // ---------------------------------------------------------------- settings

    [PostgresFact]
    public async Task Get_returns_off_default_when_no_setting_row_exists()
    {
        var setup = await ArrangeBaseAsync();
        await using var act = fixture.CreateDbContext();
        var result = await Settings(act).GetAsync(setup.SectionId, setup.HeadId);

        Assert.Equal(setup.SectionId, result.SectionId);
        Assert.False(result.GateEnabled);
        Assert.Null(result.ReminderDaysBeforeExpiry);
        Assert.Null(result.ChangedAt);
        Assert.Equal(0u, result.Version);
    }

    [PostgresFact]
    public async Task SetGate_persists_setting_signature_and_audit_and_reports_new_state()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        uint version;
        await using (var before = fixture.CreateDbContext())
            version = await before.CompetencySectionSettings.AsNoTracking()
                .Where(s => s.SectionId == setup.SectionId).Select(s => s.Version).SingleAsync();

        await using (var act = fixture.CreateDbContext(_clock))
        {
            var result = await Settings(act).SetGateAsync(setup.SectionId, true, "Qualifications entered", Password, version, setup.HeadId);
            Assert.True(result.GateEnabled);
            Assert.Equal(7, result.ReminderDaysBeforeExpiry);
            Assert.Equal("Gate head", result.ChangedByName);
        }

        await using var verify = fixture.CreateDbContext();
        var row = await verify.CompetencySectionSettings.AsNoTracking().SingleAsync(s => s.SectionId == setup.SectionId);
        Assert.True(row.GateEnabled);
        Assert.Equal(7, row.ReminderDaysBeforeExpiry);
        Assert.Equal(Now.UtcDateTime, row.ChangedAt);
        Assert.Equal(setup.HeadId, row.ChangedByUserId);
        Assert.NotEqual(version, row.Version);

        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s =>
            s.EntityType == nameof(CompetencySectionSetting) && s.EntityId == setup.SectionId &&
            s.MeaningOfSignature == SignatureMeaning.CompetencyGateChanged);
        Assert.Equal(setup.HeadId, signature.UserId);
        Assert.Equal(Now.UtcDateTime, signature.SignedAt);
        Assert.Equal("Qualifications entered", signature.Comment);

        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(CompetencySectionSetting) && a.EntityId == setup.SectionId.ToString() && a.Action == "Update");
        Assert.Equal(setup.HeadId, audit.UserId);
        Assert.Equal("Qualifications entered", audit.Reason);
        Assert.Equal(Now.UtcDateTime, audit.Timestamp);
        Assert.Contains("\"GateEnabled\":true", audit.NewValue);
        Assert.Contains("\"GateEnabled\":false", audit.PreviousValue);
    }

    [PostgresFact]
    public async Task SetGate_refuses_wrong_password_without_changing_the_setting()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        uint version;
        await using (var before = fixture.CreateDbContext())
            version = await before.CompetencySectionSettings.AsNoTracking()
                .Where(s => s.SectionId == setup.SectionId).Select(s => s.Version).SingleAsync();

        await using (var act = fixture.CreateDbContext(_clock))
            await Assert.ThrowsAsync<SignatureVerificationException>(() =>
                Settings(act).SetGateAsync(setup.SectionId, true, "Reason", "wrong", version, setup.HeadId));

        await using var verify = fixture.CreateDbContext();
        var row = await verify.CompetencySectionSettings.AsNoTracking().SingleAsync(s => s.SectionId == setup.SectionId);
        Assert.False(row.GateEnabled);
        Assert.Equal(version, row.Version);
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s =>
            s.EntityType == nameof(CompetencySectionSetting) && s.EntityId == setup.SectionId));
        Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(CompetencySectionSetting) && a.EntityId == setup.SectionId.ToString() && a.Action == "Update"));
    }

    [PostgresFact]
    public async Task SetGate_requires_a_reason()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, null, setup.HeadId);
        await using var act = fixture.CreateDbContext(_clock);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Settings(act).SetGateAsync(setup.SectionId, true, "  ", Password, null, setup.HeadId));
        Assert.Equal("Competency.GateReasonRequired", error.Code);
    }

    [PostgresFact]
    public async Task SetGate_refuses_stale_version()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, null, setup.HeadId);
        uint version;
        await using (var before = fixture.CreateDbContext())
            version = await before.CompetencySectionSettings.AsNoTracking()
                .Where(s => s.SectionId == setup.SectionId).Select(s => s.Version).SingleAsync();

        await using (var act = fixture.CreateDbContext(_clock))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Settings(act).SetGateAsync(setup.SectionId, true, "Reason", Password, version + 10, setup.HeadId));
            Assert.Equal("Competency.GateVersionStale", error.Code);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.CompetencySectionSettings.AsNoTracking()
            .Where(s => s.SectionId == setup.SectionId).Select(s => s.GateEnabled).SingleAsync());
    }

    [PostgresFact]
    public async Task SetGate_refuses_user_without_manage_permission()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, null, setup.HeadId);
        await using var act = fixture.CreateDbContext(_clock);
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            Settings(act).SetGateAsync(setup.SectionId, true, "Reason", Password, null, setup.AnalystIds[0]));
    }

    [PostgresFact]
    public async Task SetGate_refuses_section_head_of_another_section()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, null, setup.HeadId);
        await using var act = fixture.CreateDbContext(_clock);
        await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            Settings(act).SetGateAsync(setup.SectionId, true, "Reason", Password, null, setup.OtherHeadId));
    }

    [PostgresFact]
    public async Task SetGate_creates_the_setting_row_when_none_exists()
    {
        var setup = await ArrangeBaseAsync();
        await using (var act = fixture.CreateDbContext(_clock))
            await Settings(act).SetGateAsync(setup.SectionId, true, "Switching on", Password, null, setup.HeadId);

        await using var verify = fixture.CreateDbContext();
        var row = await verify.CompetencySectionSettings.AsNoTracking().SingleAsync(s => s.SectionId == setup.SectionId);
        Assert.True(row.GateEnabled);
        Assert.Equal(setup.HeadId, row.ChangedByUserId);
        Assert.Equal(Now.UtcDateTime, row.ChangedAt);
        Assert.NotNull(await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s =>
            s.EntityType == nameof(CompetencySectionSetting) && s.EntityId == setup.SectionId &&
            s.MeaningOfSignature == SignatureMeaning.CompetencyGateChanged));
        Assert.NotNull(await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(CompetencySectionSetting) && a.EntityId == setup.SectionId.ToString() && a.Action == "Create"));
    }

    // ------------------------------------------------------------------- impact

    [PostgresFact]
    public async Task Impact_previews_only_open_orders_that_would_be_refused_with_reasons()
    {
        var setup = await ArrangeBaseAsync();
        // The gate is OFF: the impact query must still evaluate as if it were on.
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        var a = setup.AnalystIds[0];
        var b = setup.AnalystIds[1];
        var c = setup.AnalystIds[2];

        var notQualifiedOrder = await AddOrderAsync(setup, a, open: true, reference: "REF-NQ");
        var expiredOrder = await AddOrderAsync(setup, b, open: true, reference: "REF-EX");
        var qualifiedOrder = await AddOrderAsync(setup, c, open: true, reference: "REF-OK");
        await AddQualificationAsync(b, setup.TestId, LabToday.AddDays(-3), setup.HeadId);
        await AddQualificationAsync(c, setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        await AddOrderAsync(setup, a, open: false, reference: "REF-CLOSED");
        await AddOrderAsync(setup, a, open: true, reference: "REF-OLD", superseded: true);
        await AddOrderAsync(setup, null, open: true, reference: "REF-UNASSIGNED");

        await using var act = fixture.CreateDbContext();
        var impact = await Settings(act).GetImpactAsync(setup.SectionId, setup.HeadId);

        Assert.Equal(setup.SectionId, impact.SectionId);
        Assert.Equal(2, impact.TotalRefusedOrders);
        Assert.Equal(new[] { a, b }, impact.Analysts.Select(x => x.AnalystUserId).OrderBy(x => x));
        var analystA = impact.Analysts.Single(x => x.AnalystUserId == a);
        var analystB = impact.Analysts.Single(x => x.AnalystUserId == b);
        Assert.Equal("Analyst A", analystA.AnalystName);
        var refusalA = Assert.Single(analystA.Orders);
        Assert.Equal(notQualifiedOrder, refusalA.TestOrderId);
        Assert.Equal("REF-NQ", refusalA.SampleReference);
        Assert.Equal("Competency.AssigneeNotQualified", Assert.Single(refusalA.Reasons).Code);
        var refusalB = Assert.Single(analystB.Orders);
        Assert.Equal(expiredOrder, refusalB.TestOrderId);
        Assert.Equal("Competency.AssigneeQualificationExpired", Assert.Single(refusalB.Reasons).Code);
        Assert.DoesNotContain(qualifiedOrder, impact.Analysts.SelectMany(x => x.Orders).Select(o => o.TestOrderId));
    }

    // ------------------------------------------------------------------- matrix

    [PostgresFact]
    public async Task Matrix_reports_all_cell_statuses_using_lab_date_boundaries()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        var a = setup.AnalystIds[0]; // qualified
        var b = setup.AnalystIds[1]; // expiring soon
        var c = setup.AnalystIds[2]; // expired
        var d = setup.AnalystIds[3]; // withdrawn
        var e = setup.AnalystIds[4]; // not qualified

        var qualA = await AddQualificationAsync(a, setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        var qualB = await AddQualificationAsync(b, setup.TestId, LabToday.AddDays(7), setup.HeadId);
        var qualC = await AddQualificationAsync(c, setup.TestId, LabToday.AddDays(-1), setup.HeadId);
        var qualD = await AddQualificationAsync(d, setup.TestId, LabToday.AddMonths(2), setup.HeadId, withdrawn: true);

        await using var act = fixture.CreateDbContext();
        var matrix = await Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), setup.HeadId);

        var test = Assert.Single(matrix.Tests);
        Assert.Equal(setup.TestId, test.TestDefinitionId);
        Assert.Null(test.Sop); // no item/SOP fact -> omitted
        // Other tests may leave department-wide analysts in the shared database: assert on this setup's own five.
        Assert.True(matrix.TotalCount >= 5);
        Assert.All(setup.AnalystIds, id => Assert.Contains(id, matrix.Analysts.Select(row => row.UserId)));
        var cells = matrix.Analysts.ToDictionary(row => row.UserId,
            row => Assert.Single(row.Cells, cell => cell.TestDefinitionId == setup.TestId));
        Assert.Equal(MatrixCellStatus.Qualified, cells[a].Status);
        Assert.Equal(qualA, cells[a].QualificationId);
        Assert.Equal(MatrixCellStatus.ExpiringSoon, cells[b].Status);
        Assert.Equal(qualB, cells[b].QualificationId);
        Assert.Equal(MatrixCellStatus.Expired, cells[c].Status);
        Assert.Equal(qualC, cells[c].QualificationId);
        Assert.Equal(MatrixCellStatus.Withdrawn, cells[d].Status);
        Assert.Equal(qualD, cells[d].QualificationId);
        Assert.Equal(MatrixCellStatus.NotQualified, cells[e].Status);
        Assert.Null(cells[e].QualificationId);
    }

    [PostgresFact]
    public async Task Matrix_filters_by_status_before_paging()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        var a = setup.AnalystIds[0]; // qualified
        var b = setup.AnalystIds[1]; // expired
        var c = setup.AnalystIds[2]; // expired

        await AddQualificationAsync(a, setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        await AddQualificationAsync(b, setup.TestId, LabToday.AddDays(-1), setup.HeadId);
        await AddQualificationAsync(c, setup.TestId, LabToday.AddDays(-2), setup.HeadId);

        await using var act = fixture.CreateDbContext();
        var page1 = await Matrix(act).GetMatrixAsync(
            setup.SectionId,
            new MatrixFilters { Status = MatrixCellStatus.Expired, Page = 1, PageSize = 1 },
            setup.HeadId);

        Assert.Equal(2, page1.TotalCount);
        var row1 = Assert.Single(page1.Analysts);
        Assert.Equal(b, row1.UserId);

        var page2 = await Matrix(act).GetMatrixAsync(
            setup.SectionId,
            new MatrixFilters { Status = MatrixCellStatus.Expired, Page = 2, PageSize = 1 },
            setup.HeadId);

        Assert.Equal(2, page2.TotalCount);
        var row2 = Assert.Single(page2.Analysts);
        Assert.Equal(c, row2.UserId);
    }

    [PostgresFact]
    public async Task Matrix_expiring_boundary_excludes_the_day_after_the_reminder_window()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        var inside = setup.AnalystIds[0];
        var outside = setup.AnalystIds[1];
        await AddQualificationAsync(inside, setup.TestId, LabToday.AddDays(7), setup.HeadId);
        await AddQualificationAsync(outside, setup.TestId, LabToday.AddDays(8), setup.HeadId);

        await using var act = fixture.CreateDbContext();
        var matrix = await Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), setup.HeadId);
        var cells = matrix.Analysts.ToDictionary(row => row.UserId, row => row.Cells[0].Status);
        Assert.Equal(MatrixCellStatus.ExpiringSoon, cells[inside]);
        Assert.Equal(MatrixCellStatus.Qualified, cells[outside]);
    }

    [PostgresFact]
    public async Task Matrix_includes_sop_status_only_when_a_sop_fact_is_available()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        var sopCode = NewCode();
        await using (var arrange = fixture.CreateDbContext())
        {
            var item = await AddItemAsync(arrange, sopCode);
            var master = await AddMasterAsync(arrange, fixture, sopCode);
            await AddRevisionAsync(arrange, fixture, master, 3, DocumentRevisionStatus.Effective, current: true);
            await arrange.TestOrders.AddAsync(new TestOrder
            {
                TestCode = setup.TestCode, SectionId = setup.SectionId,
                Sample = new Sample
                {
                    ReferenceNumber = NewCode(), ItemId = item.Id, CauseOfTestingId = setup.CauseId,
                    Category = SampleCategory.FinishedProduct, ReceivedByUserId = fixture.SeededUserId,
                    ReceivedAt = Now.UtcDateTime
                }
            });
            await arrange.SaveChangesAsync();
        }

        await using var act = fixture.CreateDbContext();
        var matrix = await Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), setup.HeadId);
        var test = Assert.Single(matrix.Tests);
        Assert.NotNull(test.Sop);
        Assert.Equal(sopCode, test.Sop!.DocumentCode);
        Assert.Equal("3", test.Sop.RevisionNumber);
    }

    [PostgresFact]
    public async Task My_returns_only_the_analysts_own_cells()
    {
        var setup = await ArrangeBaseAsync();
        var analyst = setup.AnalystIds[0];
        var other = setup.AnalystIds[1];
        await AddQualificationAsync(analyst, setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        await AddQualificationAsync(other, setup.TestId, LabToday.AddMonths(2), setup.HeadId);

        await using var act = fixture.CreateDbContext();
        var mine = await Matrix(act).GetMyAsync(analyst);

        var row = Assert.Single(mine.Analysts);
        Assert.Equal(analyst, row.UserId);
        Assert.All(row.Cells, cell => Assert.Equal(analyst, cell.UserId));
        Assert.DoesNotContain(other, mine.Analysts.Select(a => a.UserId));
        Assert.DoesNotContain(other, row.Cells.Select(c => c.UserId));
    }

    [PostgresFact]
    public async Task Matrix_page_size_is_clamped_to_the_1_to_100_range()
    {
        var setup = await ArrangeBaseAsync();
        await using var act = fixture.CreateDbContext();
        var service = Matrix(act);
        var tooBig = await service.GetMatrixAsync(setup.SectionId, new MatrixFilters { PageSize = 1000 }, setup.HeadId);
        var tooSmall = await service.GetMatrixAsync(setup.SectionId, new MatrixFilters { PageSize = 1 }, setup.HeadId);
        Assert.Equal(100, tooBig.PageSize);
        Assert.Equal(1, tooSmall.PageSize);
        var defaulted = await service.GetMatrixAsync(setup.SectionId, new MatrixFilters { PageSize = 0, Page = -3 }, setup.HeadId);
        Assert.Equal(CompetencyMatrixService.DefaultPageSize, defaulted.PageSize);
        Assert.Equal(1, defaulted.Page);
        Assert.True(tooBig.Analysts.Count >= 5);
        Assert.Single(tooSmall.Analysts);
        Assert.True(tooSmall.TotalCount >= 5);
    }

    // ---------------------------------------------------------------- reminders

    [PostgresFact]
    public async Task Reminder_query_uses_lab_dates_and_each_sections_reminder_days()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        await AddSettingAsync(setup.OtherSectionId, false, 30, setup.HeadId);
        int otherTestId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var otherTest = new TestDefinition { Code = NewCode(), DisplayName = "Other lab test", SectionId = setup.OtherSectionId };
            arrange.TestDefinitions.Add(otherTest);
            await arrange.SaveChangesAsync();
            otherTestId = otherTest.Id;
        }

        var analyst = setup.AnalystIds[0];
        var today = await AddQualificationAsync(analyst, setup.TestId, LabToday, setup.HeadId);
        var lastDay = await AddQualificationAsync(setup.AnalystIds[1], setup.TestId, LabToday.AddDays(7), setup.HeadId);
        var beyond = await AddQualificationAsync(setup.AnalystIds[2], setup.TestId, LabToday.AddDays(8), setup.HeadId);
        var expired = await AddQualificationAsync(setup.AnalystIds[3], setup.TestId, LabToday.AddDays(-1), setup.HeadId);
        var noExpiry = await AddQualificationAsync(setup.AnalystIds[4], setup.TestId, null, setup.HeadId);
        var otherSection = await AddQualificationAsync(analyst, otherTestId, LabToday.AddDays(8), setup.HeadId);

        await using var act = fixture.CreateDbContext();
        var all = await Reminders(act).GetExpiringAsync();
        var ids = all.Select(r => r.QualificationId).ToHashSet();
        Assert.Contains(today, ids);
        Assert.Contains(lastDay, ids);
        Assert.DoesNotContain(beyond, ids);
        Assert.DoesNotContain(expired, ids);
        Assert.DoesNotContain(noExpiry, ids);
        Assert.Contains(otherSection, ids);
        Assert.Equal(analyst, all.Single(r => r.QualificationId == today).UserId);
        Assert.Equal(0, all.Single(r => r.QualificationId == today).DaysRemaining);
        Assert.Equal(7, all.Single(r => r.QualificationId == lastDay).DaysRemaining);

        var mine = await Reminders(act).GetExpiringAsync(analyst);
        Assert.Equal(2, mine.Count);
        Assert.All(mine, reminder => Assert.Equal(analyst, reminder.UserId));
        Assert.Empty(await Reminders(act).GetExpiringAsync(setup.AnalystIds[4]));
    }

    [PostgresFact]
    public async Task Reminder_query_ignores_superseded_qualifications()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        // Latest record by Id governs in both directions.
        var older = await AddQualificationAsync(setup.AnalystIds[0], setup.TestId, LabToday.AddDays(3), setup.HeadId);
        var replaced = await AddQualificationAsync(setup.AnalystIds[0], setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        var stale = await AddQualificationAsync(setup.AnalystIds[1], setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        var latest = await AddQualificationAsync(setup.AnalystIds[1], setup.TestId, LabToday.AddDays(3), setup.HeadId);

        await using var act = fixture.CreateDbContext();
        var ids = (await Reminders(act).GetExpiringAsync()).Select(r => r.QualificationId).ToHashSet();
        Assert.DoesNotContain(older, ids);
        Assert.DoesNotContain(replaced, ids);
        Assert.DoesNotContain(stale, ids);
        Assert.Contains(latest, ids);
    }

    [PostgresFact]
    public async Task SetGate_refuses_a_missing_version_for_an_existing_row()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, null, setup.HeadId);
        await using (var act = fixture.CreateDbContext(_clock))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Settings(act).SetGateAsync(setup.SectionId, true, "Reason", Password, null, setup.HeadId));
            Assert.Equal("Competency.GateVersionStale", error.Code);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.CompetencySectionSettings.AsNoTracking()
            .Where(s => s.SectionId == setup.SectionId).Select(s => s.GateEnabled).SingleAsync());
        Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s =>
            s.EntityType == nameof(CompetencySectionSetting) && s.EntityId == setup.SectionId));
    }

    [PostgresFact]
    public async Task SetGate_refuses_a_version_for_a_row_that_does_not_exist_yet()
    {
        var setup = await ArrangeBaseAsync();
        await using (var act = fixture.CreateDbContext(_clock))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Settings(act).SetGateAsync(setup.SectionId, true, "Reason", Password, 5u, setup.HeadId));
            Assert.Equal("Competency.GateVersionStale", error.Code);
        }

        await using var verify = fixture.CreateDbContext();
        Assert.False(await verify.CompetencySectionSettings.AsNoTracking().AnyAsync(s => s.SectionId == setup.SectionId));
    }

    [PostgresFact]
    public async Task Matrix_refuses_a_section_head_of_another_section_and_a_user_without_view()
    {
        var setup = await ArrangeBaseAsync();
        await using var act = fixture.CreateDbContext();
        var denied = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), setup.OtherHeadId));
        Assert.Equal("Competency.SectionScopeDenied", denied.Code);

        var noView = await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
            Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), fixture.SeededUserId));
        Assert.Equal("Competency.ViewRequired", noView.Code);
    }

    [PostgresFact]
    public async Task Matrix_called_by_an_analyst_returns_only_that_analysts_row()
    {
        var setup = await ArrangeBaseAsync();
        var analyst = setup.AnalystIds[0];
        await AddQualificationAsync(analyst, setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        await AddQualificationAsync(setup.AnalystIds[1], setup.TestId, LabToday.AddMonths(2), setup.HeadId);

        await using var act = fixture.CreateDbContext();
        var matrix = await Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), analyst);
        var row = Assert.Single(matrix.Analysts);
        Assert.Equal(analyst, row.UserId);
        Assert.Equal(1, matrix.TotalCount);

        var asHead = await Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), setup.HeadId);
        Assert.True(asHead.TotalCount >= 5);
        Assert.All(setup.AnalystIds, id => Assert.Contains(id, asHead.Analysts.Select(row => row.UserId)));
    }

    [PostgresFact]
    public async Task SetReminderDays_validates_range_persists_with_audit_and_enforces_version_and_scope()
    {
        var setup = await ArrangeBaseAsync();

        await using (var act = fixture.CreateDbContext(_clock))
        {
            var created = await Settings(act).SetReminderDaysAsync(setup.SectionId, 14, null, setup.HeadId);
            Assert.Equal(14, created.ReminderDaysBeforeExpiry);
            Assert.False(created.GateEnabled);
            Assert.NotEqual(0u, created.Version);
        }

        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.CompetencySectionSettings.AsNoTracking()
                .SingleAsync(s => s.SectionId == setup.SectionId);
            Assert.Equal(14, row.ReminderDaysBeforeExpiry);
            var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a =>
                a.EntityName == nameof(CompetencySectionSetting) && a.EntityId == setup.SectionId.ToString() && a.Action == "Create");
            Assert.Equal(setup.HeadId, audit.UserId);
        }

        uint currentVersion;
        await using (var read = fixture.CreateDbContext())
            currentVersion = await read.CompetencySectionSettings.AsNoTracking()
                .Where(s => s.SectionId == setup.SectionId).Select(s => s.Version).SingleAsync();

        await using (var act = fixture.CreateDbContext(_clock))
        {
            var stale = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Settings(act).SetReminderDaysAsync(setup.SectionId, 30, currentVersion + 5, setup.HeadId));
            Assert.Equal("Competency.GateVersionStale", stale.Code);

            var tooLow = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Settings(act).SetReminderDaysAsync(setup.SectionId, 0, currentVersion, setup.HeadId));
            Assert.Equal("Competency.ReminderDaysInvalid", tooLow.Code);

            var tooHigh = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Settings(act).SetReminderDaysAsync(setup.SectionId, 366, currentVersion, setup.HeadId));
            Assert.Equal("Competency.ReminderDaysInvalid", tooHigh.Code);

            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                Settings(act).SetReminderDaysAsync(setup.SectionId, 10, currentVersion, setup.AnalystIds[0]));
            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                Settings(act).SetReminderDaysAsync(setup.SectionId, 10, currentVersion, setup.OtherHeadId));

            var cleared = await Settings(act).SetReminderDaysAsync(setup.SectionId, null, currentVersion, setup.HeadId);
            Assert.Null(cleared.ReminderDaysBeforeExpiry);
        }
    }

    [PostgresFact]
    public async Task ExportMatrixCsv_logs_DataExportLog_and_respects_section_scope_and_analyst_self_only()
    {
        var setup = await ArrangeBaseAsync();
        await AddQualificationAsync(setup.AnalystIds[0], setup.TestId, LabToday.AddMonths(2), setup.HeadId);

        await using (var act = fixture.CreateDbContext(_clock))
        {
            var (fileName, bytes) = await Matrix(act).ExportCsvAsync(setup.SectionId, new MatrixFilters(), setup.HeadId);
            Assert.EndsWith(".csv", fileName);
            var csv = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Contains(setup.TestCode, csv);
            Assert.Contains("Analyst A", csv);
            Assert.Contains("Analyst B", csv);
            Assert.Contains("Qualified", csv);
        }

        await using (var verify = fixture.CreateDbContext())
        {
            var log = await verify.DataExportLogs.AsNoTracking()
                .Where(l => l.UserId == setup.HeadId && l.ExportType == "CompetencyMatrixCsv")
                .OrderByDescending(l => l.Id)
                .FirstOrDefaultAsync();
            Assert.NotNull(log);
            Assert.True(log.RowCount >= 5);
        }

        await using (var analystAct = fixture.CreateDbContext(_clock))
        {
            var (_, analystBytes) = await Matrix(analystAct).ExportCsvAsync(setup.SectionId, new MatrixFilters(), setup.AnalystIds[0]);
            var analystCsv = System.Text.Encoding.UTF8.GetString(analystBytes);
            Assert.Contains("Analyst A", analystCsv);
            Assert.DoesNotContain("Analyst B", analystCsv);

            await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                Matrix(analystAct).ExportCsvAsync(setup.SectionId, new MatrixFilters(), setup.OtherHeadId));
        }
    }

    [PostgresFact]
    public async Task GetMatrix_and_GetMy_include_Reviewers_and_SectionHeads()
    {
        var setup = await ArrangeBaseAsync();
        int reviewerId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var reviewerRoleId = await arrange.Roles.AsNoTracking()
                .Where(r => r.Type == RoleType.Reviewer).Select(r => r.Id).FirstAsync();
            var key = NewCode();
            var reviewer = new User
            {
                RoleId = reviewerRoleId, FullName = $"Reviewer {key}",
                Username = $"reviewer-{key}", IsActive = true
            };
            arrange.Users.Add(reviewer);
            await arrange.SaveChangesAsync();
            var viewId = await arrange.Permissions.AsNoTracking()
                .Where(p => p.Code == PermissionConstants.CompetencyView).Select(p => p.Id).SingleAsync();
            if (!await arrange.RolePermissions.AnyAsync(rp => rp.RoleId == reviewerRoleId && rp.PermissionId == viewId))
                arrange.RolePermissions.Add(new RolePermission { RoleId = reviewerRoleId, PermissionId = viewId });
            arrange.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = reviewer.Id, DepartmentId = fixture.SeededDepartmentId, SectionId = setup.SectionId
            });
            await arrange.SaveChangesAsync();
            reviewerId = reviewer.Id;
        }

        await using var act = fixture.CreateDbContext();
        var sectionMatrix = await Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), setup.HeadId);
        Assert.Contains(sectionMatrix.Analysts, r => r.UserId == setup.HeadId);
        Assert.Contains(sectionMatrix.Analysts, r => r.UserId == reviewerId);

        var reviewerOwn = await Matrix(act).GetMyAsync(reviewerId);
        var ownRow = Assert.Single(reviewerOwn.Analysts);
        Assert.Equal(reviewerId, ownRow.UserId);
    }

    [PostgresFact]
    public async Task MatrixCell_includes_version_for_qualified_cell()
    {
        var setup = await ArrangeBaseAsync();
        var qualId = await AddQualificationAsync(setup.AnalystIds[0], setup.TestId, LabToday.AddMonths(2), setup.HeadId);

        await using var act = fixture.CreateDbContext();
        var matrix = await Matrix(act).GetMatrixAsync(setup.SectionId, new MatrixFilters(), setup.HeadId);
        var cell = matrix.Analysts.Single(r => r.UserId == setup.AnalystIds[0]).Cells.Single(c => c.TestDefinitionId == setup.TestId);
        Assert.Equal(qualId, cell.QualificationId);
        Assert.NotNull(cell.Version);
        Assert.NotEqual(0u, cell.Version!.Value);
    }

    [PostgresFact]
    public async Task Reminder_query_skips_an_analyst_whose_latest_record_is_withdrawn()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        var older = await AddQualificationAsync(setup.AnalystIds[0], setup.TestId, LabToday.AddDays(3), setup.HeadId);
        var withdrawn = await AddQualificationAsync(setup.AnalystIds[0], setup.TestId, LabToday.AddDays(3), setup.HeadId, withdrawn: true);

        await using var act = fixture.CreateDbContext();
        var ids = (await Reminders(act).GetExpiringAsync()).Select(r => r.QualificationId).ToHashSet();
        Assert.DoesNotContain(older, ids);
        Assert.DoesNotContain(withdrawn, ids);
    }

    // ----------------------------------------------------- notification hook

    [PostgresFact]
    public async Task Scheduler_publishes_competency_expiry_once_per_qualification_and_resolves_it_when_replaced()
    {
        var setup = await ArrangeBaseAsync();
        await AddSettingAsync(setup.SectionId, false, 7, setup.HeadId);
        var analyst = setup.AnalystIds[0];
        var expiring = await AddQualificationAsync(analyst, setup.TestId, LabToday.AddDays(3), setup.HeadId);
        var key = $"CompetencyExpiry:qualification:{expiring}";

        await RunSchedulerAsync();
        await RunSchedulerAsync(); // a second run must not duplicate

        await using (var assertDb = fixture.CreateDbContext())
        {
            var logs = await assertDb.NotificationLogs.AsNoTracking().Where(n => n.DedupeKey == key).ToListAsync();
            Assert.Equal(2, logs.Count);
            Assert.Contains(logs, n => n.UserId == analyst && n.Type == "CompetencyExpiry" && n.ResolvedAt == null);
            Assert.Contains(logs, n => n.UserId == setup.HeadId && n.Type == "CompetencyExpiry");
            Assert.All(logs, n => Assert.Equal(setup.SectionId, n.SectionId));
        }

        // Requalified for two months: the earlier record no longer governs, so the reminder is resolved.
        await AddQualificationAsync(analyst, setup.TestId, LabToday.AddMonths(2), setup.HeadId);
        await RunSchedulerAsync();

        await using var resolvedDb = fixture.CreateDbContext();
        var after = await resolvedDb.NotificationLogs.AsNoTracking().Where(n => n.DedupeKey == key).ToListAsync();
        Assert.Equal(2, after.Count);
        Assert.All(after, n => Assert.NotNull(n.ResolvedAt));
    }

    private async Task RunSchedulerAsync()
    {
        await using var db = fixture.CreateDbContext(_clock);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Frontend:Origin"] = "https://lims.example.test", ["Lab:TimeZoneId"] = "Africa/Cairo"
        }).Build();
        var scheduler = new NotificationScheduler(db,
            new NotificationPublisher(db, _clock, config, Clock),
            new NotificationRecipients(db, new SegregationOfDutiesGuard(db)),
            Clock, settings: null, config, competencyReminders: new CompetencyReminderService(db, Clock));
        await scheduler.RunAsync();
    }

    // --------------------------------------------------------------- fixtures

    private sealed record Setup(
        int SectionId, int OtherSectionId, int CauseId, int HeadId, int OtherHeadId,
        int TestId, string TestCode, IReadOnlyList<int> AnalystIds);

    private async Task<Setup> ArrangeBaseAsync()
    {
        await using var db = fixture.CreateDbContext();
        var key = NewCode();
        var section = new DocumentSection
        {
            Code = key, Name = $"Competency lab {key}", DepartmentId = fixture.SeededDepartmentId, IsActive = true
        };
        var otherSection = new DocumentSection
        {
            Code = NewCode(), Name = $"Other lab {NewCode()}", DepartmentId = fixture.SeededDepartmentId, IsActive = true
        };
        var cause = new CauseOfTesting { Name = NewCode(), IsActive = true };
        db.DocumentSections.AddRange(section, otherSection);
        db.CausesOfTesting.Add(cause);

        var manageId = await db.Permissions.AsNoTracking()
            .Where(p => p.Code == PermissionConstants.CompetencyManage).Select(p => p.Id).SingleAsync();
        var headRole = new Role { Type = RoleType.SectionHead, Name = $"head-{key}", IsActive = true };
        var otherHeadRole = new Role { Type = RoleType.SectionHead, Name = $"other-head-{key}", IsActive = true };
        var head = new User
        {
            Role = headRole, FullName = "Gate head", Username = $"head-{key}", IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        var otherHead = new User
        {
            Role = otherHeadRole, FullName = "Other head", Username = $"other-head-{key}", IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4)
        };
        var analystRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.Type == RoleType.Analyst).Select(r => r.Id).FirstAsync();
        var analysts = Enumerable.Range(0, 5).Select(i => new User
        {
            RoleId = analystRoleId, FullName = $"Analyst {(char)('A' + i)}",
            Username = $"analyst-{key}-{i}", IsActive = true
        }).ToArray();
        db.Users.AddRange(head, otherHead);
        db.Users.AddRange(analysts);
        await db.SaveChangesAsync();

        var viewId = await db.Permissions.AsNoTracking()
            .Where(p => p.Code == PermissionConstants.CompetencyView).Select(p => p.Id).SingleAsync();
        db.RolePermissions.AddRange(
            new RolePermission { RoleId = headRole.Id, PermissionId = manageId },
            new RolePermission { RoleId = otherHeadRole.Id, PermissionId = manageId },
            new RolePermission { RoleId = headRole.Id, PermissionId = viewId },
            new RolePermission { RoleId = otherHeadRole.Id, PermissionId = viewId });
        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = head.Id, DepartmentId = fixture.SeededDepartmentId, SectionId = section.Id },
            new UserOrgMembership { UserId = otherHead.Id, DepartmentId = fixture.SeededDepartmentId, SectionId = otherSection.Id });
        foreach (var analyst in analysts)
            db.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = analyst.Id, DepartmentId = fixture.SeededDepartmentId, SectionId = section.Id
            });

        var test = new TestDefinition { Code = NewCode(), DisplayName = "Gate test", SectionId = section.Id };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();

        return new(section.Id, otherSection.Id, cause.Id, head.Id, otherHead.Id, test.Id, test.Code,
            analysts.Select(a => a.Id).ToArray());
    }

    private async Task AddSettingAsync(int sectionId, bool gate, int? reminderDays, int changedBy)
    {
        await using var db = fixture.CreateDbContext();
        db.CompetencySectionSettings.Add(new CompetencySectionSetting
        {
            SectionId = sectionId, GateEnabled = gate, ReminderDaysBeforeExpiry = reminderDays,
            ChangedAt = Now.UtcDateTime, ChangedByUserId = changedBy
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> AddOrderAsync(Setup setup, int? analystId, bool open, string reference, bool superseded = false)
    {
        await using var db = fixture.CreateDbContext();
        var order = new TestOrder
        {
            TestCode = setup.TestCode,
            SectionId = setup.SectionId,
            AssignedAnalystId = analystId,
            Status = open ? ApprovalStatus.InProgress : ApprovalStatus.Approved,
            IsSuperseded = superseded,
            Sample = new Sample
            {
                ReferenceNumber = reference, CauseOfTestingId = setup.CauseId,
                Category = SampleCategory.FinishedProduct, ReceivedByUserId = fixture.SeededUserId,
                ReceivedAt = Now.UtcDateTime
            }
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<int> AddQualificationAsync(
        int userId, int definitionId, DateOnly? expiresOn, int signedBy, bool withdrawn = false)
    {
        await using var db = fixture.CreateDbContext();
        var id = await fixture.CreateSequenceHelper(db).ReserveIdAsync<AnalystQualification>();
        var qualification = new AnalystQualification
        {
            Id = id,
            UserId = userId,
            TestDefinitionId = definitionId,
            Basis = QualificationBasis.ObservedPractice,
            Evidence = "Observed the method",
            QualifiedOn = LabToday.AddMonths(-1),
            ExpiresOn = expiresOn,
            SignedByUserId = signedBy,
            Signature = new ElectronicSignature
            {
                UserId = signedBy, EntityType = nameof(AnalystQualification), EntityId = id,
                MeaningOfSignature = SignatureMeaning.AnalystQualified, SignedAt = Now.UtcDateTime.AddDays(-4)
            }
        };
        db.AnalystQualifications.Add(qualification);
        await db.SaveChangesAsync();
        if (withdrawn)
        {
            qualification.WithdrawnOn = Now.UtcDateTime;
            qualification.WithdrawnByUserId = signedBy;
            qualification.WithdrawnReason = "Evidence withdrawn";
            qualification.WithdrawnSignature = new ElectronicSignature
            {
                UserId = signedBy, EntityType = nameof(AnalystQualification), EntityId = id,
                SignedAt = Now.UtcDateTime,
                MeaningOfSignature = SignatureMeaning.AnalystQualificationWithdrawn,
                Comment = qualification.WithdrawnReason
            };
            await db.SaveChangesAsync();
        }
        return id;
    }
}
