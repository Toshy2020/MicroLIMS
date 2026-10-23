using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class CalendarEventServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);
    private static byte[] SamplePdf => new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

    private sealed class World
    {
        public MicroLimsDbContext Db = null!;
        public InMemoryFileStorageService Storage = null!;
        public MaterialDocumentFileValidator Validator = null!;
        public CalendarEventService Service = null!;
        public User Analyst = null!;
        public EquipmentInventory Equipment = null!;
    }

    private static async Task<World> BuildAsync()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var w = new World
        {
            Db = new MicroLimsDbContext(options),
            Storage = new InMemoryFileStorageService(),
            Validator = new MaterialDocumentFileValidator(30 * 1024 * 1024L)
        };

        var timeProvider = new FixedTimeProvider(Now);

        w.Analyst = new User
        {
            Username = "analyst1",
            FullName = "Analyst One",
            RoleId = 1,
            PasswordHash = "hash"
        };
        w.Db.Users.Add(w.Analyst);

        w.Equipment = new EquipmentInventory
        {
            Code = "EQ-001",
            InstrumentType = "Autoclave",
            ManufacturerName = "Tuttnauer",
            SerialNumber = "SN-9999",
            Location = "Micro Lab 1"
        };
        w.Db.EquipmentInventories.Add(w.Equipment);

        await w.Db.SaveChangesAsync();

        w.Service = new CalendarEventService(w.Db, w.Storage, w.Validator, timeProvider: timeProvider);
        return w;
    }

    [Fact]
    public async Task Requires_date_or_month_not_both()
    {
        var w = await BuildAsync();

        // 1. Neither date nor month set
        var exNoDateOrMonth = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CreateAsync(new CreateCalendarEventRequest(
                CalendarEventType.VendorVisit,
                "Vendor Maintenance Visit",
                Date: null,
                Month: null,
                Note: null,
                EquipmentInventoryId: null), w.Analyst.Id));
        Assert.Contains("date or month", exNoDateOrMonth.Message, StringComparison.OrdinalIgnoreCase);

        // 2. Both date and month set
        var exBoth = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CreateAsync(new CreateCalendarEventRequest(
                CalendarEventType.VendorVisit,
                "Vendor Maintenance Visit",
                Date: new DateOnly(2026, 10, 25),
                Month: "2026-10",
                Note: null,
                EquipmentInventoryId: null), w.Analyst.Id));
        Assert.Contains("date or month", exBoth.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Invalid month format (e.g. not yyyy-MM)
        var exBadMonth = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CreateAsync(new CreateCalendarEventRequest(
                CalendarEventType.Audit,
                "Annual Internal Audit",
                Date: null,
                Month: "2026/10",
                Note: null,
                EquipmentInventoryId: null), w.Analyst.Id));
        Assert.Contains("yyyy-MM", exBadMonth.Message, StringComparison.OrdinalIgnoreCase);

        // 4. Missing / whitespace title
        var exNoTitle = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CreateAsync(new CreateCalendarEventRequest(
                CalendarEventType.Audit,
                "   ",
                Date: new DateOnly(2026, 10, 25),
                Month: null,
                Note: null,
                EquipmentInventoryId: null), w.Analyst.Id));
        Assert.Contains("title", exNoTitle.Message, StringComparison.OrdinalIgnoreCase);

        // 5. Title exceeding 200 characters
        var exLongTitle = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CreateAsync(new CreateCalendarEventRequest(
                CalendarEventType.Audit,
                new string('X', 201),
                Date: new DateOnly(2026, 10, 25),
                Month: null,
                Note: null,
                EquipmentInventoryId: null), w.Analyst.Id));
        Assert.Contains("200", exLongTitle.Message, StringComparison.OrdinalIgnoreCase);

        // 6. Valid date event
        var dateEv = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.VendorVisit,
            "Valid Day Event",
            Date: new DateOnly(2026, 10, 25),
            Month: null,
            Note: "Important note",
            EquipmentInventoryId: w.Equipment.Id), w.Analyst.Id);
        Assert.Equal(new DateOnly(2026, 10, 25), dateEv.Date);
        Assert.Null(dateEv.Month);
        Assert.Equal("EQ-001", dateEv.EquipmentCode);

        // 7. Valid month event
        var monthEv = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.Audit,
            "Valid Month Event",
            Date: null,
            Month: "2026-11",
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);
        Assert.Null(monthEv.Date);
        Assert.Equal("2026-11", monthEv.Month);
    }

    [Fact]
    public async Task Pt_event_is_report_pending_until_document_attached()
    {
        var w = await BuildAsync();

        // PT event created with no documents
        var ptEvent = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.ProficiencyTesting,
            "FAPAS Water Microbiology PT",
            Date: new DateOnly(2026, 10, 30),
            Month: null,
            Note: "Round 4",
            EquipmentInventoryId: null), w.Analyst.Id);

        Assert.True(ptEvent.ReportPending);
        Assert.Empty(ptEvent.Documents);

        // Other event types are never ReportPending
        var vendorEvent = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.VendorVisit,
            "Vendor Visit",
            Date: new DateOnly(2026, 10, 30),
            Month: null,
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);
        Assert.False(vendorEvent.ReportPending);

        // Upload a document to the PT event
        var docSummary = await w.Service.UploadDocumentAsync(ptEvent.Id, new UploadCalendarEventDocumentRequest(
            "pt-result-report.pdf",
            "application/pdf",
            SamplePdf), w.Analyst.Id);

        Assert.NotNull(docSummary);
        Assert.Equal("pt-result-report.pdf", docSummary.OriginalFileName);
        Assert.Equal(".pdf", docSummary.FileExtension);
        Assert.Equal(MaterialDocumentStatus.Current, docSummary.Status);

        // Retrieve event - ReportPending is now false
        var updatedPtEvent = await w.Service.GetByIdAsync(ptEvent.Id);
        Assert.False(updatedPtEvent.ReportPending);
        Assert.Single(updatedPtEvent.Documents);
        Assert.Equal(docSummary.Id, updatedPtEvent.Documents[0].Id);
        Assert.Equal("Analyst One", updatedPtEvent.Documents[0].UploadedByName);
    }

    [Fact]
    public async Task Cancel_requires_reason_and_keeps_row()
    {
        var w = await BuildAsync();

        var ev = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.BreakdownRepair,
            "Emergency pump replacement",
            Date: new DateOnly(2026, 10, 24),
            Month: null,
            Note: "Pump leaking",
            EquipmentInventoryId: w.Equipment.Id), w.Analyst.Id);

        // Empty reason rejected
        var exNoReason = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CancelAsync(ev.Id, "   ", w.Analyst.Id));
        Assert.Contains("reason", exNoReason.Message, StringComparison.OrdinalIgnoreCase);

        // Cancel with valid reason
        var cancelled = await w.Service.CancelAsync(ev.Id, "Vendor could not supply parts", w.Analyst.Id);
        Assert.Equal(CalendarEventStatus.Cancelled, cancelled.Status);

        // Row remains in database (not hard deleted)
        var rowInDb = await w.Db.CalendarEvents.FindAsync(ev.Id);
        Assert.NotNull(rowInDb);
        Assert.Equal(CalendarEventStatus.Cancelled, rowInDb.Status);
        Assert.Equal("Vendor could not supply parts", rowInDb.CancelReason);

        // Cancelled event cannot be edited
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.UpdateAsync(ev.Id, new UpdateCalendarEventRequest(
                CalendarEventType.BreakdownRepair,
                "New Title",
                new DateOnly(2026, 10, 25),
                null,
                null,
                null), w.Analyst.Id));

        // Cancelled event cannot be cancelled again
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.CancelAsync(ev.Id, "Cancel again", w.Analyst.Id));

        // Documents cannot be attached to a cancelled event
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            w.Service.UploadDocumentAsync(ev.Id, new UploadCalendarEventDocumentRequest(
                "report.pdf", "application/pdf", SamplePdf), w.Analyst.Id));
    }

    [Fact]
    public async Task List_returns_month_events_overlapping_range()
    {
        var w = await BuildAsync();

        // Event in October (Month)
        var octMonth = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.Audit,
            "October Audit",
            Date: null,
            Month: "2026-10",
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);

        // Event in November (Month)
        var novMonth = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.VendorVisit,
            "November Vendor Visit",
            Date: null,
            Month: "2026-11",
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);

        // Date event inside October
        var octDay = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.Other,
            "October Day Event",
            Date: new DateOnly(2026, 10, 15),
            Month: null,
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);

        // Date event in December
        var decDay = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.Other,
            "December Day Event",
            Date: new DateOnly(2026, 12, 5),
            Month: null,
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);

        // Query only October: 2026-10-01 to 2026-10-31
        var octList = await w.Service.GetCalendarEventsAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var octIds = octList.Select(e => e.Id).ToHashSet();
        Assert.Contains(octMonth.Id, octIds);
        Assert.Contains(octDay.Id, octIds);
        Assert.DoesNotContain(novMonth.Id, octIds);
        Assert.DoesNotContain(decDay.Id, octIds);

        // Query cross-month: 2026-10-20 to 2026-11-10
        var crossList = await w.Service.GetCalendarEventsAsync(new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 10));
        var crossIds = crossList.Select(e => e.Id).ToHashSet();
        Assert.Contains(octMonth.Id, crossIds); // October month event overlaps Oct 20
        Assert.Contains(novMonth.Id, crossIds); // November month event overlaps Nov 10
        Assert.DoesNotContain(octDay.Id, crossIds); // Day event is Oct 15 (before Oct 20)
        Assert.DoesNotContain(decDay.Id, crossIds);
    }

    [Fact]
    public async Task Document_storage_follows_expected_key_pattern_and_sha256()
    {
        var w = await BuildAsync();

        var ev = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.VendorVisit,
            "Vendor Visit",
            Date: new DateOnly(2026, 10, 28),
            Month: null,
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);

        var doc = await w.Service.UploadDocumentAsync(ev.Id, new UploadCalendarEventDocumentRequest(
            "Service-Report.pdf",
            "application/pdf",
            SamplePdf), w.Analyst.Id);

        var expectedKey = $"calendar-event-documents/{ev.Id}/{doc.Id}.pdf";
        Assert.True(w.Storage.Files.ContainsKey(expectedKey));
        Assert.Equal(SamplePdf, w.Storage.Files[expectedKey]);

        // Content retrieval verifies SHA-256 and returns content
        var (meta, content) = await w.Service.GetDocumentContentAsync(doc.Id);
        Assert.Equal("Service-Report.pdf", meta.OriginalFileName);
        Assert.Equal(SamplePdf, content);
    }

    [Fact]
    public async Task Concurrency_checks_stale_if_match_version()
    {
        var w = await BuildAsync();

        var ev = await w.Service.CreateAsync(new CreateCalendarEventRequest(
            CalendarEventType.Audit,
            "Internal Audit",
            Date: new DateOnly(2026, 10, 28),
            Month: null,
            Note: null,
            EquipmentInventoryId: null), w.Analyst.Id);

        // Simulate If-Match from stale form
        w.Db.ExpectedVersion = ev.Version + 10;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            w.Service.UpdateAsync(ev.Id, new UpdateCalendarEventRequest(
                CalendarEventType.Audit,
                "Updated Audit Title",
                new DateOnly(2026, 10, 29),
                null,
                null,
                null), w.Analyst.Id));
    }
}
