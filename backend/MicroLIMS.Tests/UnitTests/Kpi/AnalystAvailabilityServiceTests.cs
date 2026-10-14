using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using Xunit;
using static MicroLIMS.Tests.UnitTests.Kpi.WorkloadTestData;

namespace MicroLIMS.Tests.UnitTests.Kpi;

public class AnalystAvailabilityServiceTests
{
    private static SaveAvailabilityRequest Req(int user = Alice, int fromDay = 14, int toDay = 16, decimal fraction = 0.5m, string reason = "Training course") =>
        new(user, new DateOnly(2026, 10, fromDay), new DateOnly(2026, 10, toDay), fraction, reason);

    [Fact]
    public async Task Create_SavesTheRow_NamesBothPeople_AndRecordsAConfigurationEvent()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        var dto = await TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(), Sam);

        Assert.Equal("Alice Analyst", dto.UserName);
        Assert.Equal("Sam Head", dto.EnteredByName);
        Assert.Equal(Now.UtcDateTime, dto.EnteredAt);
        var audit = await db.AuditLogs.SingleAsync(a => a.ActionCode == AnalystAvailabilityService.ChangedActionCode);
        Assert.Equal("Training course", audit.Reason);
        Assert.Equal(dto.Id.ToString(), audit.EntityId);
    }

    [Theory]
    [InlineData(14, 16, 1.5, "Training course")]  // fraction above 1
    [InlineData(14, 16, -0.1, "Training course")] // fraction below 0
    [InlineData(14, 16, 0.555, "Training course")] // more than two decimals
    [InlineData(16, 14, 0.5, "Training course")]  // ends before it starts
    [InlineData(14, 16, 0.5, "   ")]              // reason required
    public async Task Create_RejectsInvalidInput(int fromDay, int toDay, double fraction, string reason)
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(fromDay: fromDay, toDay: toDay, fraction: (decimal)fraction, reason: reason), Sam));
        Assert.Empty(db.AnalystAvailabilities);
    }

    [Fact]
    public async Task Create_ForAnUnknownUser_IsRejected()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(user: 999), Sam));
    }

    [Fact]
    public async Task Create_OverlappingAnExistingWindow_IsRejected_ButAdjacentIsAllowed()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        await service.CreateAsync(Req(fromDay: 14, toDay: 16), Sam);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Req(fromDay: 16, toDay: 17), Sam));
        await service.CreateAsync(Req(fromDay: 17, toDay: 18), Sam); // starts the day after the first ends

        Assert.Equal(2, await db.AnalystAvailabilities.CountAsync());
    }

    [Fact]
    public async Task Create_ForAnAnalystOutsideTheEditorsSections_IsForbidden()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        // Sam works in section 1; Dana only in section 2.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).CreateAsync(Req(user: Dana), Sam));
    }

    [Fact]
    public async Task Update_CorrectsTheRowInPlace_AndAuditsEachChange()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        var created = await service.CreateAsync(Req(), Sam);

        var updated = await service.UpdateAsync(created.Id, Req(fraction: 0.25m, reason: "Correction: half day booked in error"), Sam);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(0.25m, updated.FractionAvailable);
        Assert.Equal("Correction: half day booked in error", updated.Reason);
        Assert.Equal(1, await db.AnalystAvailabilities.CountAsync()); // never silently removed or duplicated
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.ActionCode == AnalystAvailabilityService.ChangedActionCode));
    }

    [Fact]
    public async Task Update_CannotMoveAnEntryToAnotherAnalyst()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        var created = await service.CreateAsync(Req(), Sam);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, Req(user: Bob), Sam));
    }

    [Fact]
    public async Task Update_OfAnUnknownEntry_IsNotFound()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            TestServiceFactory.AnalystAvailability(db, Clock()).UpdateAsync(999, Req(), Sam));
    }

    [Fact]
    public async Task List_WithoutViewTeam_ReturnsOnlyTheCallersRows_AndRefusesAnotherUser()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        await service.CreateAsync(Req(user: Alice), Sam);
        await service.CreateAsync(Req(user: Bob), Sam);

        var own = await service.ListAsync(Alice, canViewTeam: false, userId: null, from: null, to: null);

        Assert.Single(own);
        Assert.Equal(Alice, own[0].UserId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ListAsync(Alice, canViewTeam: false, userId: Bob, from: null, to: null));
    }

    [Fact]
    public async Task List_WithViewTeam_IsLimitedToTheViewersSections()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        db.AnalystAvailabilities.AddRange(
            new AnalystAvailability { UserId = Alice, FromDate = new(2026, 10, 14), ToDate = new(2026, 10, 14), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam },
            new AnalystAvailability { UserId = Dana, FromDate = new(2026, 10, 14), ToDate = new(2026, 10, 14), FractionAvailable = 0, Reason = "Leave", EnteredByUserId = Sam });
        await db.SaveChangesAsync();
        var service = TestServiceFactory.AnalystAvailability(db, Clock());

        var rows = await service.ListAsync(Sam, canViewTeam: true, userId: null, from: null, to: null);

        Assert.Equal(new[] { Alice }, rows.Select(r => r.UserId)); // Dana works only in section 2
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ListAsync(Sam, canViewTeam: true, userId: Dana, from: null, to: null));
    }

    [Fact]
    public async Task List_FiltersByDateWindow()
    {
        await using var db = NewDb();
        await SeedBaselineAsync(db);
        var service = TestServiceFactory.AnalystAvailability(db, Clock());
        await service.CreateAsync(Req(fromDay: 14, toDay: 16), Sam);
        await service.CreateAsync(Req(fromDay: 26, toDay: 27), Sam);

        var rows = await service.ListAsync(Sam, true, Alice, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 31));

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 10, 26), rows[0].FromDate);
    }

    [Fact]
    public void SaveRequest_SerialisesExactlyTheFieldNamesTheFrontendSends_AndDatesAsIsoStrings()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(Req(), options);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(new[] { "fractionAvailable", "fromDate", "reason", "toDate", "userId" },
            doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("2026-10-14", doc.RootElement.GetProperty("fromDate").GetString());
        Assert.Equal(new DateOnly(2026, 10, 16), JsonSerializer.Deserialize<SaveAvailabilityRequest>(json, options)!.ToDate);
    }
}
