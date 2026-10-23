using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

public class EquipmentStatusAndDocumentTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        // Seed test user as a System Administrator (unrestricted scope) -
        // these tests exercise status/document traceability, not lab
        // scoping, so the caller must see equipment regardless of
        // SectionId (rows here are seeded without one).
        if (!db.Users.Any())
        {
            db.Roles.Add(new Role { Id = 1, Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true });
            db.Users.Add(new User
            {
                Id = 1,
                FullName = "Mohamed Analyst",
                Username = "mohamed",
                PasswordHash = "hash",
                RoleId = 1,
                IsActive = true
            });
            db.SaveChanges();
        }

        return db;
    }

    private static EquipmentDocumentService BuildDocService(MicroLimsDbContext db, IFileStorageService? storage = null)
    {
        var validator = new MaterialDocumentFileValidator(maxFileSizeBytes: 26_214_400L);
        return new EquipmentDocumentService(db, storage ?? new InMemoryFileStorageService(),
            validator, NullLogger<EquipmentDocumentService>.Instance);
    }

    private static async Task<EquipmentInventory> SeedEquipment(
        MicroLimsDbContext db,
        EquipmentOperationalStatus status = EquipmentOperationalStatus.InService,
        string code = "INC-001")
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var eq = new EquipmentInventory
        {
            InstrumentType = "Incubator",
            ManufacturerName = "Memmert",
            SerialNumber = "SN-10023",
            FirmwareVersion = "v1.0.4",
            Code = code,
            Location = "Microbiology Lab",
            CalibrationDueDate = DateTime.UtcNow.AddMonths(6),
            Status = status,
            SectionId = section.Id,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1
        };
        db.EquipmentInventories.Add(eq);
        await db.SaveChangesAsync();
        return eq;
    }

    private static byte[] MakePdf() =>
        new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4

    private static byte[] MakePng() =>
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header

    // =========================================================================
    // 1. Equipment Status Traceability Tests
    // =========================================================================

    [Fact]
    public async Task StatusChange_InServiceToOutOfService_WithComment_RecordsHistory()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.InService);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.OutOfService,
            StatusChangeComment: "Sent to vendor for annual calibration and preventative maintenance.", SectionId: eq.SectionId);

        await service.UpdateAsync(eq.Id, request, 1);

        var updated = await db.EquipmentInventories.FindAsync(eq.Id);
        Assert.NotNull(updated);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, updated.Status);

        var history = await service.GetStatusHistoryAsync(eq.Id, 1);
        Assert.Single(history);
        Assert.Equal(EquipmentOperationalStatus.InService, history[0].PreviousStatus);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, history[0].NewStatus);
        Assert.Equal("Sent to vendor for annual calibration and preventative maintenance.", history[0].Comment);
        Assert.Equal("Mohamed Analyst", history[0].ChangedByName);
    }

    [Fact]
    public async Task StatusChange_OutOfServiceToInService_WithComment_RecordsSecondHistory()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.OutOfService);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.InService,
            StatusChangeComment: "Calibration completed and verified against certificate CAL-2026-044.", SectionId: eq.SectionId);

        await service.UpdateAsync(eq.Id, request, 1);

        var history = await service.GetStatusHistoryAsync(eq.Id, 1);
        Assert.Single(history);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, history[0].PreviousStatus);
        Assert.Equal(EquipmentOperationalStatus.InService, history[0].NewStatus);
    }

    [Fact]
    public async Task StatusChange_WithoutComment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.InService);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.OutOfService,
            StatusChangeComment: null, SectionId: eq.SectionId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(eq.Id, request, 1));
        Assert.Contains("comment explaining the reason for changing the operational status is required", ex.Message);
    }

    [Fact]
    public async Task StatusChange_WithWhitespaceOnlyComment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.InService);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.Retired,
            StatusChangeComment: "    \t  \n  ", SectionId: eq.SectionId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(eq.Id, request, 1));
        Assert.Contains("comment explaining the reason for changing the operational status is required", ex.Message);
    }

    [Fact]
    public async Task UpdateMetadata_WithoutStatusChange_DoesNotRequireComment_AndDoesNotRecordHistory()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.InService);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, "Updated Manufacturer", eq.SerialNumber, "v2.0.0",
            eq.Code, "Updated Room 105", eq.CalibrationDueDate, EquipmentOperationalStatus.InService,
            StatusChangeComment: null, SectionId: eq.SectionId);

        await service.UpdateAsync(eq.Id, request, 1);

        var updated = await db.EquipmentInventories.FindAsync(eq.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Manufacturer", updated.ManufacturerName);
        Assert.Equal("v2.0.0", updated.FirmwareVersion);

        var history = await service.GetStatusHistoryAsync(eq.Id, 1);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetStatusHistory_NonExistentEquipment_ThrowsNotFoundException()
    {
        await using var db = NewDb();
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetStatusHistoryAsync(99999, 1));
    }

    // =========================================================================
    // 2. Controlled Equipment Document (Calibration Certificate) Tests
    // =========================================================================

    [Fact]
    public async Task Upload_PdfCertificate_AcceptedAndCurrent()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var docService = BuildDocService(db);

        var doc = await docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Calibration-Cert-2026.pdf",
            "application/pdf",
            MakePdf()), 1);

        Assert.Equal(MaterialDocumentStatus.Current, doc.Status);
        Assert.Equal(EquipmentDocumentType.CalibrationCertificate, doc.DocumentType);
        Assert.Equal(".pdf", doc.FileExtension);
        Assert.NotEmpty(doc.ContentSha256);
    }

    [Fact]
    public async Task Upload_PngCertificate_Accepted()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var docService = BuildDocService(db);

        var doc = await docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Certificate-Scan.png",
            "image/png",
            MakePng()), 1);

        Assert.Equal(MaterialDocumentStatus.Current, doc.Status);
        Assert.Equal(".png", doc.FileExtension);
    }

    [Fact]
    public async Task Upload_InvalidFileType_Rejected()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var docService = BuildDocService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
                EquipmentDocumentType.CalibrationCertificate,
                "malicious.exe",
                "application/x-msdownload",
                new byte[] { 0x4D, 0x5A, 0x00, 0x00 }), 1));

        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetContent_ValidCertificate_ReturnsContentAndLogsAccess()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var storage = new InMemoryFileStorageService();
        var docService = BuildDocService(db, storage);

        var doc = await docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Cert.pdf",
            "application/pdf",
            MakePdf()), 1);

        var (meta, content) = await docService.GetContentAsync(doc.Id, eq.Id, 1);

        Assert.Equal(doc.Id, meta.Id);
        Assert.Equal(MakePdf(), content);

        var log = await db.EquipmentDocumentAccessLogs.FirstOrDefaultAsync(l => l.DocumentId == doc.Id);
        Assert.NotNull(log);
        Assert.Equal(EquipmentDocumentAccessAction.Download, log.Action);
    }

    [Fact]
    public async Task GetContent_TamperedContent_FailsIntegrityCheck()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var storage = new InMemoryFileStorageService();
        var docService = BuildDocService(db, storage);

        var doc = await docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Cert.pdf",
            "application/pdf",
            MakePdf()), 1);

        // Tamper with the underlying storage directly
        await storage.SaveAsync($"equipment-documents/{eq.Id}/{doc.Id}.pdf", new byte[] { 0x00, 0x00, 0x00, 0x00 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.GetContentAsync(doc.Id, eq.Id, 1));

        Assert.Contains("integrity check failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Supersede_MarksOldSuperseded_AndCreatesNewCurrent()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var docService = BuildDocService(db);

        var doc1 = await docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Cert-v1.pdf",
            "application/pdf",
            MakePdf()), 1);

        var doc2 = await docService.SupersedeAsync(doc1.Id, eq.Id, new SupersedeEquipmentDocumentRequest(
            "Cert-v2.pdf",
            "application/pdf",
            MakePdf(),
            "Annual recalibration certificate update"), 1);

        Assert.Equal(MaterialDocumentStatus.Current, doc2.Status);

        var reloadedDoc1 = await db.EquipmentDocuments.FindAsync(doc1.Id);
        Assert.NotNull(reloadedDoc1);
        Assert.Equal(MaterialDocumentStatus.Superseded, reloadedDoc1.Status);
        Assert.Equal(doc2.Id, reloadedDoc1.SupersededByDocumentId);
        Assert.Equal("Annual recalibration certificate update", reloadedDoc1.SupersessionReason);
    }

    [Fact]
    public async Task Supersede_WithoutReason_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var docService = BuildDocService(db);

        var doc1 = await docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Cert-v1.pdf",
            "application/pdf",
            MakePdf()), 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.SupersedeAsync(doc1.Id, eq.Id, new SupersedeEquipmentDocumentRequest(
                "Cert-v2.pdf",
                "application/pdf",
                MakePdf(),
                "   "), 1));
    }

    [Fact]
    public async Task Void_MarksDocumentVoidedWithReason()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db);
        var docService = BuildDocService(db);

        var doc = await docService.UploadAsync(eq.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Cert.pdf",
            "application/pdf",
            MakePdf()), 1);

        var voided = await docService.VoidAsync(doc.Id, eq.Id, new VoidEquipmentDocumentRequest(
            "Incorrect certificate scanned from balance instead of incubator"), 1);

        Assert.Equal(MaterialDocumentStatus.Voided, voided.Status);
        Assert.Equal("Incorrect certificate scanned from balance instead of incubator", voided.VoidReason);
    }

    [Fact]
    public async Task EquipmentDocument_Isolation_CannotAccessWithWrongEquipmentId()
    {
        await using var db = NewDb();
        var eq1 = await SeedEquipment(db, code: "EQ-001");
        var eq2 = await SeedEquipment(db, code: "EQ-002");
        var docService = BuildDocService(db);

        var doc = await docService.UploadAsync(eq1.Id, new UploadEquipmentDocumentRequest(
            EquipmentDocumentType.CalibrationCertificate,
            "Cert.pdf",
            "application/pdf",
            MakePdf()), 1);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            docService.GetContentAsync(doc.Id, eq2.Id, 1));
    }

    [Fact]
    public async Task CreateAsync_Refuses_Duplicate_SerialNumber_From_Same_Manufacturer()
    {
        await using var db = NewDb();
        var existing = await SeedEquipment(db, EquipmentOperationalStatus.InService, code: "EQ-EXISTING");
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var reqDup = new SaveEquipmentInventoryRequest(
            "Incubator",
            "  memmert  ",
            "  sn-10023  ",
            "v1.0.0",
            "EQ-NEW",
            "Lab 2",
            DateTime.UtcNow.AddMonths(6),
            EquipmentOperationalStatus.InService,
            SectionId: existing.SectionId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(reqDup, 1));
        Assert.Equal($"Instrument {existing.Code} already has serial number sn-10023 from memmert.", ex.Message);

        // Different manufacturer with same serial is allowed
        var reqDiffMan = new SaveEquipmentInventoryRequest(
            "Incubator",
            "Binder",
            "SN-10023",
            "v1.0.0",
            "EQ-DIFF-MAN",
            "Lab 2",
            DateTime.UtcNow.AddMonths(6),
            EquipmentOperationalStatus.InService,
            SectionId: existing.SectionId);
        var createdDiff = await service.CreateAsync(reqDiffMan, 1);
        Assert.NotNull(createdDiff);

        // Empty serial is allowed
        var reqEmptySerial = new SaveEquipmentInventoryRequest(
            "Incubator",
            "Memmert",
            "",
            "v1.0.0",
            "EQ-NO-SERIAL",
            "Lab 2",
            DateTime.UtcNow.AddMonths(6),
            EquipmentOperationalStatus.InService,
            SectionId: existing.SectionId);
        var createdEmpty = await service.CreateAsync(reqEmptySerial, 1);
        Assert.NotNull(createdEmpty);
    }

    [Fact]
    public async Task CreateAsync_Allows_Duplicate_SerialNumber_If_Other_Is_Retired()
    {
        await using var db = NewDb();
        var retired = await SeedEquipment(db, EquipmentOperationalStatus.Retired, code: "EQ-RETIRED");
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var req = new SaveEquipmentInventoryRequest(
            "Incubator",
            "Memmert",
            "SN-10023",
            "v1.0.0",
            "EQ-REPLACEMENT",
            "Lab 2",
            DateTime.UtcNow.AddMonths(6),
            EquipmentOperationalStatus.InService,
            SectionId: retired.SectionId);

        var created = await service.CreateAsync(req, 1);
        Assert.NotNull(created);
    }

    [Fact]
    public async Task UpdateAsync_Refuses_Duplicate_SerialNumber_From_Same_Manufacturer()
    {
        await using var db = NewDb();
        var eq1 = await SeedEquipment(db, EquipmentOperationalStatus.InService, code: "EQ-001");
        var section = TestServiceFactory.EnsureMicroSection(db);
        var eq2 = new EquipmentInventory
        {
            InstrumentType = "Incubator",
            ManufacturerName = "Memmert",
            SerialNumber = "SN-UNIQUE-999",
            Code = "EQ-002",
            Location = "Microbiology Lab",
            Status = EquipmentOperationalStatus.InService,
            SectionId = section.Id,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1
        };
        db.EquipmentInventories.Add(eq2);
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        // Updating eq2 to eq1's serial should be refused
        var reqDup = new SaveEquipmentInventoryRequest(
            eq2.InstrumentType,
            "Memmert",
            "SN-10023",
            eq2.FirmwareVersion,
            eq2.Code,
            eq2.Location,
            eq2.CalibrationDueDate,
            eq2.Status,
            SectionId: eq2.SectionId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(eq2.Id, reqDup, 1));
        Assert.Equal($"Instrument {eq1.Code} already has serial number SN-10023 from Memmert.", ex.Message);

        // Updating eq1 keeping its own serial should succeed (ignores itself)
        var reqSelf = new SaveEquipmentInventoryRequest(
            eq1.InstrumentType,
            eq1.ManufacturerName,
            eq1.SerialNumber,
            eq1.FirmwareVersion,
            eq1.Code,
            "New Room 204",
            eq1.CalibrationDueDate,
            eq1.Status,
            SectionId: eq1.SectionId);

        await service.UpdateAsync(eq1.Id, reqSelf, 1);
        var refreshed = await db.EquipmentInventories.FindAsync(eq1.Id);
        Assert.Equal("New Room 204", refreshed!.Location);
    }

    [Fact]
    public async Task Placeholder_serials_are_not_duplicates()
    {
        await using var db = NewDb();
        var existing = await SeedEquipment(db, EquipmentOperationalStatus.InService, code: "LAF-01");
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var req1 = new SaveEquipmentInventoryRequest(
            "LAF Cabinet",
            "Rich-Tek",
            "N/A",
            null,
            existing.Code,
            "Cleanroom 1",
            null,
            EquipmentOperationalStatus.InService,
            SectionId: existing.SectionId);
        await service.UpdateAsync(existing.Id, req1, 1);

        // Second instrument from Rich-Tek with "n/a" should save fine
        var req2 = new SaveEquipmentInventoryRequest(
            "LAF Cabinet",
            "Rich-Tek",
            "n/a",
            null,
            "LAF-02",
            "Cleanroom 2",
            null,
            EquipmentOperationalStatus.InService,
            SectionId: existing.SectionId);
        var created = await service.CreateAsync(req2, 1);
        Assert.NotNull(created);
        Assert.Equal("n/a", created.SerialNumber);

        // Updating the second instrument with another placeholder "-" should also save fine
        var req2Update = new SaveEquipmentInventoryRequest(
            "LAF Cabinet",
            "Rich-Tek",
            " - ",
            null,
            "LAF-02",
            "Cleanroom 2",
            null,
            EquipmentOperationalStatus.InService,
            SectionId: existing.SectionId);
        await service.UpdateAsync(created.Id, req2Update, 1);

        var refreshed = await db.EquipmentInventories.FindAsync(created.Id);
        Assert.Equal(" - ", refreshed!.SerialNumber);
    }
}
