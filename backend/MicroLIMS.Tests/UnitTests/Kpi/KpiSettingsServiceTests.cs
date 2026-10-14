using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Shared.Exceptions;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class KpiSettingsServiceTests
{
    private static UpdateKpiSettingsRequest Req(decimal hours = 8m, int mask = 31, int horizon = 3, string reason = "Site works Mon-Fri, 8 h days") =>
        new(hours, mask, horizon, reason);

    [Fact]
    public async Task Get_WithNoRow_ReturnsDocumentedDefaultsFlaggedAsDefault()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var dto = await TestServiceFactory.KpiSettings(db, Clock()).GetAsync(1, Sam);

        Assert.True(dto.IsDefault);
        Assert.Equal(6.0m, dto.HoursPerAnalystDay);
        Assert.Equal(31, dto.WorkingWeekdays);
        Assert.Equal(5, dto.LoadHorizonDays);
    }

    [Fact]
    public async Task Update_CreatesTheRow_AndRecordsAConfigurationAuditEventWithTheReason()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var dto = await TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(1, Req(), Sam);

        Assert.False(dto.IsDefault);
        Assert.Equal(8m, dto.HoursPerAnalystDay);
        var row = await db.KpiSettings.SingleAsync();
        Assert.Equal(Sam, row.UpdatedByUserId);
        Assert.Equal(Now.UtcDateTime, row.UpdatedAt);
        var audit = await db.AuditLogs.Include(a => a.Changes)
            .SingleAsync(a => a.ActionCode == KpiSettingsService.SettingsChangedActionCode);
        Assert.Equal("Site works Mon-Fri, 8 h days", audit.Reason);
        Assert.Equal("1", audit.EntityId);
        Assert.Contains(audit.Changes, c => c.FieldName == "HoursPerAnalystDay" && c.PreviousValue == "6.00" && c.NewValue == "8.00");
    }

    [Fact]
    public async Task Update_OfAnExistingRow_EditsInPlace()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.KpiSettings(db, Clock());
        await service.UpdateAsync(1, Req(), Sam);

        await service.UpdateAsync(1, Req(hours: 7.5m, horizon: 10, reason: "Revised after time study"), Sam);

        var row = await db.KpiSettings.SingleAsync();
        Assert.Equal(7.5m, row.HoursPerAnalystDay);
        Assert.Equal(10, row.LoadHorizonDays);
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.ActionCode == KpiSettingsService.SettingsChangedActionCode));
    }

    [Theory]
    [InlineData(0.0, 31, 3, "r")]     // hours must be > 0
    [InlineData(24.5, 31, 3, "r")]    // hours must be <= 24
    [InlineData(6.123, 31, 3, "r")]   // at most two decimals
    [InlineData(6.0, 0, 3, "r")]      // at least one working weekday
    [InlineData(6.0, 128, 3, "r")]    // only seven weekday bits
    [InlineData(6.0, 31, 0, "r")]     // horizon >= 1
    [InlineData(6.0, 31, 31, "r")]    // horizon <= 30
    [InlineData(6.0, 31, 3, "  ")]    // a reason is required
    public async Task Update_RejectsInvalidValues(double hours, int mask, int horizon, string reason)
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(1, new((decimal)hours, mask, horizon, reason), Sam));
        Assert.Empty(db.KpiSettings);
    }

    [Fact]
    public async Task Update_WithAStaleIfMatchVersion_IsRefused()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.KpiSettings(db, Clock());
        await service.UpdateAsync(1, Req(), Sam);

        db.ExpectedVersion = 7; // the form was loaded at a version that no longer exists

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(1, Req(hours: 5m), Sam));
    }

    [Fact]
    public async Task Update_OfASectionOutsideTheCallersScope_IsForbidden()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        // Dana only works in section 2.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(1, Req(), Dana));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).GetAsync(1, Dana));
    }

    [Fact]
    public async Task Update_OfAnUnknownSection_IsNotFound()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            TestServiceFactory.KpiSettings(db, Clock()).UpdateAsync(99, Req(), Sam));
    }

    [Fact]
    public void UpdateRequest_SerialisesExactlyTheFieldNamesTheFrontendSends()
    {
        // The legacy weight update bound "weight" to a differently named property; pin the wire names.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(Req(), options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(new[] { "hoursPerAnalystDay", "loadHorizonDays", "reason", "workingWeekdays" },
            doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        var back = JsonSerializer.Deserialize<UpdateKpiSettingsRequest>(json, options)!;
        Assert.Equal(8m, back.HoursPerAnalystDay);
        Assert.Equal(31, back.WorkingWeekdays);
        Assert.Equal(3, back.LoadHorizonDays);
    }
}
