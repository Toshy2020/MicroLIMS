using System.Data.Common;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using static MicroLIMS.Tests.IntegrationTests.ItemSopResolverPostgresTests;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SopRevisionCapturePostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTimeOffset CapturedAt = new(2037, 2, 3, 4, 5, 6, TimeSpan.Zero);
    private static readonly DateTime RevisionEffective = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] CapturedColumns =
    [
        "SopCaptureStatus", "SopDocumentMasterId", "SopDocumentCode", "SopDocumentTitle",
        "SopRevisionId", "SopRevisionNumber", "SopEffectiveDateUtc", "SopCapturedAtUtc"
    ];

    [PostgresFact]
    public Task Resolver_status_is_stored_on_the_test_order() =>
        PostgresCases.RunAsync(
            new (string Scenario, SopCaptureStatus Expected)[]
            {
                ("effective", SopCaptureStatus.Captured),
                ("blank", SopCaptureStatus.NoSopNumber),
                ("missing", SopCaptureStatus.NotFound),
                ("draft", SopCaptureStatus.NoEffectiveRevision)
            },
            c => ResolverStatusCaseAsync(c.Scenario, c.Expected));

    private async Task ResolverStatusCaseAsync(string scenario, SopCaptureStatus expected)
    {
        var setup = await ArrangeAsync(scenario);

        await using (var act = fixture.CreateDbContext())
        {
            var order = await act.TestOrders.SingleAsync(o => o.Id == setup.OrderId);
            await CaptureAsync(act, order);
            await act.SaveChangesAsync();
        }

        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == setup.OrderId);
        Assert.Equal(expected, stored.SopCaptureStatus);
        Assert.Equal(CapturedAt.UtcDateTime, stored.SopCapturedAtUtc);
        if (expected == SopCaptureStatus.Captured)
        {
            Assert.Equal(setup.MasterId, stored.SopDocumentMasterId);
            Assert.Equal(setup.Code, stored.SopDocumentCode);
            Assert.Equal(setup.Code, stored.SopDocumentTitle);
            Assert.Equal(setup.RevisionId, stored.SopRevisionId);
            Assert.Equal("3", stored.SopRevisionNumber);
            Assert.Equal(RevisionEffective, stored.SopEffectiveDateUtc);
        }
        else
        {
            Assert.Null(stored.SopDocumentMasterId);
            Assert.Null(stored.SopDocumentCode);
            Assert.Null(stored.SopDocumentTitle);
            Assert.Null(stored.SopRevisionId);
            Assert.Null(stored.SopRevisionNumber);
            Assert.Null(stored.SopEffectiveDateUtc);
        }
    }

    [PostgresFact]
    public async Task No_capture_for_a_sample_without_an_item()
    {
        var setup = await ArrangeAsync("no-item");

        await using (var act = fixture.CreateDbContext())
        {
            var order = await act.TestOrders.SingleAsync(o => o.Id == setup.OrderId);
            await CaptureAsync(act, order);
            await act.SaveChangesAsync();
        }

        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == setup.OrderId);
        Assert.Null(stored.SopCaptureStatus);
        foreach (var column in CapturedColumns)
            Assert.Null(ReadColumn(stored, column));
    }

    [PostgresFact]
    public async Task Already_captured_order_is_not_overwritten()
    {
        var setup = await ArrangeAsync("effective");
        await using (var arrange = fixture.CreateDbContext())
        {
            var order = await arrange.TestOrders.SingleAsync(o => o.Id == setup.OrderId);
            order.SopCaptureStatus = SopCaptureStatus.Captured;
            order.SopDocumentMasterId = 111;
            order.SopDocumentCode = "ORIGINAL";
            order.SopDocumentTitle = "Original title";
            order.SopRevisionId = 222;
            order.SopRevisionNumber = "1";
            order.SopEffectiveDateUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            order.SopCapturedAtUtc = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            await arrange.SaveChangesAsync();
        }

        await using (var act = fixture.CreateDbContext())
        {
            var order = await act.TestOrders.SingleAsync(o => o.Id == setup.OrderId);
            await CaptureAsync(act, order);
            await act.SaveChangesAsync();
        }

        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == setup.OrderId);
        Assert.Equal(SopCaptureStatus.Captured, stored.SopCaptureStatus);
        Assert.Equal(111, stored.SopDocumentMasterId);
        Assert.Equal("ORIGINAL", stored.SopDocumentCode);
        Assert.Equal("Original title", stored.SopDocumentTitle);
        Assert.Equal(222, stored.SopRevisionId);
        Assert.Equal("1", stored.SopRevisionNumber);
        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), stored.SopEffectiveDateUtc);
        Assert.Equal(new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc), stored.SopCapturedAtUtc);
    }

    [PostgresFact]
    public async Task Capture_then_save_persists_every_column_and_uses_the_injected_clock()
    {
        var setup = await ArrangeAsync("effective");

        await using (var act = fixture.CreateDbContext())
        {
            var order = await act.TestOrders.SingleAsync(o => o.Id == setup.OrderId);
            await CaptureAsync(act, order);
            Assert.Equal(SopCaptureStatus.Captured, order.SopCaptureStatus);
            Assert.Equal(CapturedAt.UtcDateTime, order.SopCapturedAtUtc);
            await act.SaveChangesAsync();
        }

        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == setup.OrderId);
        Assert.Equal(SopCaptureStatus.Captured, stored.SopCaptureStatus);
        Assert.Equal(setup.MasterId, stored.SopDocumentMasterId);
        Assert.Equal(setup.Code, stored.SopDocumentCode);
        Assert.Equal(setup.Code, stored.SopDocumentTitle);
        Assert.Equal(setup.RevisionId, stored.SopRevisionId);
        Assert.Equal("3", stored.SopRevisionNumber);
        Assert.Equal(RevisionEffective, stored.SopEffectiveDateUtc);
        Assert.Equal(CapturedAt.UtcDateTime, stored.SopCapturedAtUtc);
    }

    [PostgresFact]
    public Task Trigger_rejects_any_captured_column_change_after_capture() =>
        PostgresCases.RunAsync(CapturedColumns, TriggerRejectsColumnChangeCaseAsync);

    private async Task TriggerRejectsColumnChangeCaseAsync(string column)
    {
        var setup = await ArrangeAsync("effective");
        await CaptureAndSaveAsync(setup.OrderId);

        await using var db = fixture.CreateDbContext();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var command = conn.CreateCommand();
        var value = column switch
        {
            "SopCaptureStatus" => "'NoSopNumber'",
            "SopDocumentMasterId" or "SopRevisionId" => "999999",
            "SopEffectiveDateUtc" or "SopCapturedAtUtc" => "'2030-01-01T00:00:00Z'",
            _ => "'CHANGED'"
        };
        command.CommandText = $"UPDATE \"TestOrders\" SET \"{column}\" = {value} WHERE \"Id\" = {setup.OrderId}";
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    [PostgresFact]
    public async Task Null_to_value_is_allowed_once()
    {
        var setup = await ArrangeAsync("effective");
        var assignments = string.Join(", ", CapturedColumns.Select(c => c switch
        {
            "SopCaptureStatus" => "\"SopCaptureStatus\" = 'Captured'",
            "SopDocumentMasterId" => "\"SopDocumentMasterId\" = 111",
            "SopDocumentCode" => "\"SopDocumentCode\" = 'CODE'",
            "SopDocumentTitle" => "\"SopDocumentTitle\" = 'Title'",
            "SopRevisionId" => "\"SopRevisionId\" = 222",
            "SopRevisionNumber" => "\"SopRevisionNumber\" = '4'",
            "SopEffectiveDateUtc" => "\"SopEffectiveDateUtc\" = '2027-01-01T00:00:00Z'",
            _ => "\"SopCapturedAtUtc\" = '2027-01-02T00:00:00Z'"
        }));

        await using var db = fixture.CreateDbContext();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var command = conn.CreateCommand();
        command.CommandText = $"UPDATE \"TestOrders\" SET {assignments} WHERE \"Id\" = {setup.OrderId}";
        await command.ExecuteNonQueryAsync();

        command.CommandText = $"UPDATE \"TestOrders\" SET \"SopDocumentCode\" = 'LATER' WHERE \"Id\" = {setup.OrderId}";
        await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == setup.OrderId);
        Assert.Equal(SopCaptureStatus.Captured, stored.SopCaptureStatus);
        Assert.Equal("CODE", stored.SopDocumentCode);
    }

    [PostgresFact]
    public async Task Updating_another_column_after_capture_still_works()
    {
        var setup = await ArrangeAsync("effective");
        await CaptureAndSaveAsync(setup.OrderId);

        await using var db = fixture.CreateDbContext();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var command = conn.CreateCommand())
        {
            command.CommandText = $"UPDATE \"TestOrders\" SET \"OutsourcedToLab\" = 'OTHER LAB' WHERE \"Id\" = {setup.OrderId}";
            await command.ExecuteNonQueryAsync();
        }

        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == setup.OrderId);
        Assert.Equal("OTHER LAB", stored.OutsourcedToLab);
        Assert.Equal(SopCaptureStatus.Captured, stored.SopCaptureStatus);
    }

    [PostgresFact]
    public async Task Migration_leaves_existing_rows_with_null_capture_columns()
    {
        var setup = await ArrangeAsync("effective");

        await using var assert = fixture.CreateDbContext();
        var conn = assert.Database.GetDbConnection();
        await conn.OpenAsync();

        await using (var columns = conn.CreateCommand())
        {
            columns.CommandText = """
                SELECT column_name, is_nullable, column_default
                FROM information_schema.columns
                WHERE table_name = 'TestOrders' AND column_name = ANY(@names)
                """;
            columns.Parameters.Add(new NpgsqlParameter("names", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text) { Value = CapturedColumns });
            await using var reader = await columns.ExecuteReaderAsync();
            var found = new List<string>();
            while (await reader.ReadAsync())
            {
                found.Add(reader.GetString(0));
                Assert.Equal("YES", reader.GetString(1));
                Assert.True(reader.IsDBNull(2), $"{reader.GetString(0)} must have no default");
            }
            Assert.Equal(CapturedColumns.Order(StringComparer.Ordinal), found.Order(StringComparer.Ordinal));
        }

        await using (var row = conn.CreateCommand())
        {
            row.CommandText = $"SELECT {string.Join(", ", CapturedColumns.Select(c => $"\"{c}\""))} FROM \"TestOrders\" WHERE \"Id\" = {setup.OrderId}";
            await using var reader = await row.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            for (var i = 0; i < CapturedColumns.Length; i++)
                Assert.True(reader.IsDBNull(i), $"{CapturedColumns[i]} must stay null");
        }
    }

    [PostgresFact]
    public async Task Display_uses_stored_values_not_live_lookup()
    {
        var setup = await ArrangeAsync("effective");
        await CaptureAndSaveAsync(setup.OrderId);
        await using (var arrange = fixture.CreateDbContext())
        {
            var order = await arrange.TestOrders.Include(o => o.Sample).ThenInclude(s => s!.Item)
                .SingleAsync(o => o.Id == setup.OrderId);
            var replacement = await AddMasterAsync(arrange, fixture, NewCode());
            var revision = await AddRevisionAsync(arrange, fixture, replacement, 4, DocumentRevisionStatus.Effective, current: true);
            revision.EffectiveDate = CapturedAt.UtcDateTime.AddDays(1);
            order.Sample!.Item!.SopNumber = replacement.CompanyDocumentCode;
            await arrange.SaveChangesAsync();
        }

        var dto = await ReadDisplayAsync(setup.OrderId);
        Assert.Equal(setup.MasterId, dto.SopCapture?.DocumentMasterId);
        Assert.Equal(setup.RevisionId, dto.SopCapture?.RevisionId);
        Assert.Equal(setup.Code, dto.SopCapture?.DocumentCode);
        Assert.Equal(setup.Code, dto.SopCapture?.Title);
        Assert.Equal("3", dto.SopCapture?.RevisionNumber);
        Assert.Equal(RevisionEffective, dto.SopCapture?.EffectiveDateUtc);
        Assert.Equal(CapturedAt.UtcDateTime, dto.SopCapture?.CapturedAtUtc);
        Assert.Null(dto.NewerSopRevision);

        await using var workspace = fixture.CreateDbContext();
        var sampleId = await GetSampleIdAsync(setup.OrderId);
        var service = new TestingWorkspaceService(workspace, new UserSectionScopeService(workspace),
            new FixedTimeProvider(CapturedAt.AddDays(2)));
        var workspaceOrder = Assert.Single((await service.GetSampleAsync(sampleId))!.AssignedTests);
        Assert.Equal(dto.SopCapture, workspaceOrder.SopCapture);
        Assert.Null(workspaceOrder.NewerSopRevision);
    }

    [PostgresFact]
    public async Task Old_tests_show_not_recorded()
    {
        var setup = await ArrangeAsync("effective");
        var summary = await ReadSummaryAsync(setup.OrderId);
        var dto = Assert.Single(summary.TestOrders);
        Assert.Null(dto.SopCapture);
        Assert.Null(dto.NewerSopRevision);
        Assert.Contains("  SOP not recorded", SampleSummaryService.BuildReportLines(summary));
    }

    [PostgresFact]
    public async Task Later_same_document_revision_shows_notice_for_open_test()
    {
        var setup = await ArrangeAsync("effective");
        await CaptureAndSaveAsync(setup.OrderId);
        await ArrangeNewerRevisionAsync(setup, completed: false);

        var dto = await ReadDisplayAsync(setup.OrderId);
        Assert.Equal("3", dto.SopCapture?.RevisionNumber);
        Assert.Equal("4", dto.NewerSopRevision?.RevisionNumber);
    }

    [PostgresFact]
    public async Task Revision_effective_at_completion_does_not_show_notice()
    {
        var setup = await ArrangeAsync("effective");
        await CaptureAndSaveAsync(setup.OrderId);
        await ArrangeNewerRevisionAsync(setup, completed: true);

        Assert.Null((await ReadDisplayAsync(setup.OrderId)).NewerSopRevision);
    }

    [PostgresFact]
    public async Task Revision_effective_before_completion_shows_notice()
    {
        var setup = await ArrangeAsync("effective");
        await CaptureAndSaveAsync(setup.OrderId);
        await ArrangeNewerRevisionAsync(setup, completed: true, completionOffsetHours: 1);

        var dto = await ReadDisplayAsync(setup.OrderId);
        Assert.Equal(new NewerSopRevisionDto("4", CapturedAt.UtcDateTime.AddDays(1)), dto.NewerSopRevision);
    }

    [PostgresFact]
    public async Task Certificate_lists_each_distinct_revision()
    {
        var setup = await ArrangeAsync("effective");
        var sampleId = await GetSampleIdAsync(setup.OrderId);
        await using (var arrange = fixture.CreateDbContext())
        {
            var sectionId = await arrange.TestOrders.AsNoTracking()
                .Where(o => o.Id == setup.OrderId).Select(o => o.SectionId).SingleAsync();
            arrange.TestOrders.AddRange(
                CapturedOrder(sampleId, sectionId, "SOP-B", "1", 4),
                CapturedOrder(sampleId, sectionId, "SOP-A", "2", 2),
                CapturedOrder(sampleId, sectionId, "SOP-A", "1", 1),
                CapturedOrder(sampleId, sectionId, "SOP-A", "2", 2),
                CapturedOrder(sampleId, sectionId, "SOP-C", "3", 5, superseded: true));
            await arrange.SaveChangesAsync();
        }

        var summary = await ReadSummaryAsync(setup.OrderId);
        Assert.Equal(new[] { ("SOP-A", "1"), ("SOP-A", "2"), ("SOP-B", "1") },
            summary.Certificate.Sample.MethodDocuments.Select(m => (m.DocumentCode, m.RevisionNumber)));
        Assert.All(summary.Certificate.Sample.MethodDocuments, m => Assert.Equal(RevisionEffective, m.EffectiveDateUtc));

        var report = ReportDocumentMapper.ForSample(summary, TimeZoneInfo.Utc);
        var header = Assert.Single(report.Blocks.OfType<ListBlock>(), b => b.Label == "methods");
        Assert.Equal(summary.Certificate.Sample.MethodDocuments.Count, header.Rows.Count);
        Assert.Contains(report.Blocks.OfType<CardBlock>(), card =>
            card.MetaLines.Any(line => line.StartsWith("SOP-A rev. 2", StringComparison.Ordinal)));
    }

    [PostgresFact]
    public async Task Certificate_omits_unresolved()
    {
        var setup = await ArrangeAsync("missing");
        await CaptureAndSaveAsync(setup.OrderId);

        var summary = await ReadSummaryAsync(setup.OrderId);
        Assert.Equal(nameof(SopCaptureStatus.NotFound), Assert.Single(summary.TestOrders).SopCapture?.Status);
        Assert.Empty(summary.Certificate.Sample.MethodDocuments);
    }

    private static TestOrder CapturedOrder(int sampleId, int sectionId, string code, string number, int revisionId,
        bool superseded = false) => new()
    {
        SampleId = sampleId,
        SectionId = sectionId,
        TestCode = NewCode(),
        IsSuperseded = superseded,
        SopCaptureStatus = SopCaptureStatus.Captured,
        SopDocumentMasterId = int.MaxValue - revisionId,
        SopDocumentCode = code,
        SopDocumentTitle = code,
        SopRevisionId = int.MaxValue - revisionId,
        SopRevisionNumber = number,
        SopEffectiveDateUtc = RevisionEffective,
        SopCapturedAtUtc = CapturedAt.UtcDateTime
    };

    private async Task ArrangeNewerRevisionAsync(Setup setup, bool completed, int completionOffsetHours = 0)
    {
        await using var arrange = fixture.CreateDbContext();
        var master = await arrange.DocumentMasters.SingleAsync(m => m.Id == setup.MasterId);
        var previous = await arrange.DocumentRevisions.SingleAsync(r => r.Id == setup.RevisionId);
        previous.RevisionStatus = DocumentRevisionStatus.Superseded;
        await arrange.SaveChangesAsync();
        var revision = await AddRevisionAsync(arrange, fixture, master, 4, DocumentRevisionStatus.Effective, current: true);
        revision.EffectiveDate = CapturedAt.UtcDateTime.AddDays(1);
        if (completed)
        {
            var order = await arrange.TestOrders.SingleAsync(o => o.Id == setup.OrderId);
            order.CurrentStep = WorkflowStep.Ready;
            order.Status = ApprovalStatus.ResultEntered;
            order.WorkflowHistory.Add(new WorkflowHistory
            {
                FromStep = WorkflowStep.Running,
                ToStep = WorkflowStep.Ready,
                Timestamp = revision.EffectiveDate.Value.AddHours(completionOffsetHours),
                PerformedByUserId = fixture.SeededUserId
            });
        }
        await arrange.SaveChangesAsync();
    }

    private async Task<TestOrderSummaryDetailDto> ReadDisplayAsync(int orderId) =>
        Assert.Single((await ReadSummaryAsync(orderId)).TestOrders);

    private async Task<int> GetSampleIdAsync(int orderId)
    {
        await using var assert = fixture.CreateDbContext();
        return await assert.TestOrders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => o.SampleId).SingleAsync();
    }

    private async Task<SampleSummaryDto> ReadSummaryAsync(int orderId)
    {
        var sampleId = await GetSampleIdAsync(orderId);
        await using var act = fixture.CreateDbContext();
        var service = new SampleSummaryService(act, null!, null!, new ReviewGateService(act, null!),
            new OosGate(act, new OosOpeningRule(act)), new FixedTimeProvider(CapturedAt.AddDays(2)));
        return Assert.IsType<SampleSummaryDto>(await service.GetSummaryAsync(sampleId));
    }

    private sealed record Setup(int OrderId, int? MasterId, int? RevisionId, string? Code);

    private async Task<Setup> ArrangeAsync(string scenario)
    {
        await using var db = fixture.CreateDbContext();
        var section = new DocumentSection
        {
            Code = NewCode(), Name = $"SOP capture lab {NewCode()}", DepartmentId = fixture.SeededDepartmentId
        };
        db.DocumentSections.Add(section);
        var cause = new CauseOfTesting { Name = NewCode(), IsActive = true };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        string? code = null;
        int? itemId = null;
        DocumentMaster? master = null;
        DocumentRevision? revision = null;
        if (scenario != "no-item")
        {
            code = scenario == "blank" ? "   " : NewCode();
            itemId = (await AddItemAsync(db, code)).Id;
            if (scenario is "effective" or "draft")
            {
                master = await AddMasterAsync(db, fixture, code);
                revision = await AddRevisionAsync(db, fixture, master, 3,
                    scenario == "draft" ? DocumentRevisionStatus.Draft : DocumentRevisionStatus.Effective,
                    current: true);
            }
        }

        var order = new TestOrder
        {
            TestCode = NewCode(),
            SectionId = section.Id,
            Sample = new Sample
            {
                ReferenceNumber = NewCode(),
                ItemId = itemId,
                CauseOfTestingId = cause.Id,
                Category = SampleCategory.FinishedProduct,
                ReceivedByUserId = fixture.SeededUserId,
                ReceivedAt = CapturedAt.UtcDateTime
            }
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return new(order.Id, master?.Id, revision?.Id, code);
    }

    private async Task CaptureAndSaveAsync(int orderId)
    {
        await using var act = fixture.CreateDbContext();
        var order = await act.TestOrders.SingleAsync(o => o.Id == orderId);
        await CaptureAsync(act, order);
        await act.SaveChangesAsync();
    }

    private static Task CaptureAsync(MicroLimsDbContext db, TestOrder order) =>
        new SopRevisionCapture(db, new ItemSopResolver(db), new FixedTimeProvider(CapturedAt))
            .CaptureIfNeededAsync(order);

    private static object? ReadColumn(TestOrder order, string column) => column switch
    {
        "SopCaptureStatus" => order.SopCaptureStatus,
        "SopDocumentMasterId" => order.SopDocumentMasterId,
        "SopDocumentCode" => order.SopDocumentCode,
        "SopDocumentTitle" => order.SopDocumentTitle,
        "SopRevisionId" => order.SopRevisionId,
        "SopRevisionNumber" => order.SopRevisionNumber,
        "SopEffectiveDateUtc" => order.SopEffectiveDateUtc,
        _ => order.SopCapturedAtUtc
    };
}
