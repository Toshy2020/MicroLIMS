using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SopCaptureStartDatePostgresTests(PostgresTestFixture fixture)
{
    private const string StartDateKey = "sopCapture.startDateUtc";

    [PostgresFact]
    public async Task Migration_RecordSopCaptureStartDate_InsertsSingleUtcRow_IsIdempotent_AndDownRemovesIt()
    {
        await ScratchPostgresDatabase.RunAsync(async connectionString =>
        {
            await using var db = ScratchPostgresDatabase.NewContext(connectionString);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_RecordSopCaptureStartDate", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];

            await db.Database.MigrateAsync(before);
            Assert.False(await db.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == StartDateKey));

            var lowerBound = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.Database.MigrateAsync(target);
            var upperBound = DateTimeOffset.UtcNow.AddMinutes(1);

            var rows = await db.SystemSettings.AsNoTracking().Where(s => s.Key == StartDateKey).ToListAsync();
            var row = Assert.Single(rows);
            Assert.True(
                DateTimeOffset.TryParse(row.Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed),
                $"Expected ISO-8601 UTC timestamp, got '{row.Value}'.");
            Assert.Equal(TimeSpan.Zero, parsed.Offset);
            Assert.InRange(parsed, lowerBound, upperBound);

            // Re-running the migration's Up SQL must not duplicate or fail.
            var upSql = db.GetService<IMigrator>().GenerateScript(before, target);
            var insertOnlySql = string.Join("\n",
                upSql.Split('\n').Where(l => !l.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)));
            await db.Database.ExecuteSqlRawAsync(insertOnlySql);
            Assert.Equal(1, await db.SystemSettings.AsNoTracking().CountAsync(s => s.Key == StartDateKey));

            // Down removes the row.
            await db.Database.MigrateAsync(before);
            Assert.False(await db.SystemSettings.AsNoTracking().AnyAsync(s => s.Key == StartDateKey));
        });
    }

    [PostgresFact]
    public async Task SystemSettingsService_DoesNotList_OrAllowEditing_SopCaptureStartDate()
    {
        await using var db = fixture.CreateDbContext();
        var cache = new SystemSettingsCache();
        var signatures = new ElectronicSignatureService(db);
        var service = new SystemSettingsService(db, cache, signatures);

        var overview = await service.GetOverviewAsync();
        Assert.DoesNotContain(overview.Settings, s => s.Key == StartDateKey);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(StartDateKey, "2020-01-01T00:00:00Z", "attempt edit", fixture.SeededUserId, "Admin@123", null));
    }

    [PostgresFact]
    public async Task PreCapture_Order_Shows_NotRecordedBeforeDate_In_Report_Certificate_And_Dtos()
    {
        var fixedStartUtc = new DateTime(2026, 11, 26, 14, 0, 0, DateTimeKind.Utc);
        await SetStartDateAsync(fixedStartUtc);

        int orderId;
        int sampleId;
        int userId = fixture.SeededUserId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var s = TitrationScenario.Seed(arrange, new TitrationScenario.Options { ExistingUserId = userId });
            orderId = s.Order.Id;
            sampleId = s.Order.SampleId;

            arrange.TestOrders.Add(new TestOrder
            {
                SampleId = sampleId,
                SectionId = s.Order.SectionId,
                TestCode = $"SCSD-{Guid.NewGuid():N}"[..16],
                SopCaptureStatus = SopCaptureStatus.NotFound,
                SopCapturedAtUtc = fixedStartUtc
            });
            await arrange.SaveChangesAsync();
        }

        await using var act = fixture.CreateDbContext();
        ISopCaptureStartDateReader reader = new SopCaptureStartDateReader(act);

        var summaryService = new SampleSummaryService(
            act, null!, null!, new ReviewGateService(act, null!),
            new OosGate(act, new OosOpeningRule(act)), TimeProvider.System, null, reader);
        var summary = await summaryService.GetSummaryAsync(sampleId);
        Assert.NotNull(summary);
        Assert.Equal(fixedStartUtc, summary!.SopCaptureStartDateUtc);

        const string expectedText = "Not recorded (before 26-Nov-2026)";
        Assert.Contains(summary.Certificate.Sample.MethodDocuments, m => m.PreCaptureStartDateUtc == fixedStartUtc);

        var report = ReportDocumentMapper.ForSample(summary, TimeZoneInfo.Utc);
        var methodsBlock = Assert.Single(report.Blocks.OfType<ListBlock>(), b => b.Label == "methods");
        Assert.Contains(methodsBlock.Rows, r => r.Right == expectedText);
        Assert.Contains(report.Blocks.OfType<CardBlock>(), c => c.MetaLines.Contains(expectedText));
        Assert.Contains(report.Blocks.OfType<CardBlock>(), c => c.MetaLines.Contains("SOP not recorded: SOP not found in Document Control"));

        var lines = SampleSummaryService.BuildReportLines(summary, TimeZoneInfo.Utc);
        Assert.Contains($"Method: {expectedText}", lines);
        Assert.Contains($"  {expectedText}", lines);
        Assert.Contains("  SOP not recorded: SOP not found in Document Control", lines);

        var engine = TestServiceFactory.TestWorkflow(act);
        var currentStep = new CurrentStepViewService(act, engine, TimeProvider.System, reader);
        var payload = await currentStep.GetAsync(orderId);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("sopCapture").ValueKind);
        Assert.Equal(fixedStartUtc, doc.RootElement.GetProperty("sopCaptureStartDateUtc").GetDateTime().ToUniversalTime());

        var workspace = new TestingWorkspaceService(act, new StubSectionScope(_ => null), TimeProvider.System, null, reader);
        var sampleDto = await workspace.GetSampleAsync(sampleId, userId);
        Assert.NotNull(sampleDto);
        Assert.Equal(fixedStartUtc, sampleDto!.SopCaptureStartDateUtc);
    }

    [PostgresFact]
    public async Task PreCapture_Date_Is_Converted_To_Lab_TimeZone_Near_Midnight()
    {
        // 23:30 UTC on Nov 26 is 01:30 on Nov 27 in UTC+2.
        var nearMidnightUtc = new DateTime(2026, 11, 26, 23, 30, 0, DateTimeKind.Utc);
        await SetStartDateAsync(nearMidnightUtc);

        int sampleId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var s = TitrationScenario.Seed(arrange, new TitrationScenario.Options { ExistingUserId = fixture.SeededUserId });
            sampleId = s.Order.SampleId;
        }

        await using var act = fixture.CreateDbContext();
        var summaryService = new SampleSummaryService(
            act, null!, null!, new ReviewGateService(act, null!),
            new OosGate(act, new OosOpeningRule(act)), TimeProvider.System, null,
            new SopCaptureStartDateReader(act));
        var summary = await summaryService.GetSummaryAsync(sampleId);
        Assert.NotNull(summary);

        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");
        var report = ReportDocumentMapper.ForSample(summary!, plusTwo);
        var methodsBlock = Assert.Single(report.Blocks.OfType<ListBlock>(), b => b.Label == "methods");
        Assert.Contains(methodsBlock.Rows, r => r.Right == "Not recorded (before 27-Nov-2026)");
        Assert.Contains(report.Blocks.OfType<CardBlock>(), c => c.MetaLines.Contains("Not recorded (before 27-Nov-2026)"));
    }

    private async Task SetStartDateAsync(DateTime utcInstant)
    {
        await using var db = fixture.CreateDbContext();
        var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == StartDateKey);
        var iso = utcInstant.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        if (existing is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = StartDateKey,
                Value = iso,
                ValueType = SystemSettingValueType.String,
                Category = "System",
                IsGxpRelevant = false,
                Description = "test",
                CurrentVersion = 1
            });
        }
        else
        {
            existing.Value = iso;
        }
        await db.SaveChangesAsync();
    }
}
