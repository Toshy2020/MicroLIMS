using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
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

        // Seed test user
        if (!db.Users.Any())
        {
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
        var service = new EquipmentInventoryService(db);

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.OutOfService,
            StatusChangeComment: "Sent to vendor for annual calibration and preventative maintenance.");

        await service.UpdateAsync(eq.Id, request, 1);

        var updated = await db.EquipmentInventories.FindAsync(eq.Id);
        Assert.NotNull(updated);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, updated.Status);

        var history = await service.GetStatusHistoryAsync(eq.Id);
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
        var service = new EquipmentInventoryService(db);

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.InService,
            StatusChangeComment: "Calibration completed and verified against certificate CAL-2026-044.");

        await service.UpdateAsync(eq.Id, request, 1);

        var history = await service.GetStatusHistoryAsync(eq.Id);
        Assert.Single(history);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, history[0].PreviousStatus);
        Assert.Equal(EquipmentOperationalStatus.InService, history[0].NewStatus);
    }

    [Fact]
    public async Task StatusChange_WithoutComment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.InService);
        var service = new EquipmentInventoryService(db);

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.OutOfService,
            StatusChangeComment: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(eq.Id, request, 1));
        Assert.Contains("comment explaining the reason for changing the operational status is required", ex.Message);
    }

    [Fact]
    public async Task StatusChange_WithWhitespaceOnlyComment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.InService);
        var service = new EquipmentInventoryService(db);

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, eq.ManufacturerName, eq.SerialNumber, eq.FirmwareVersion,
            eq.Code, eq.Location, eq.CalibrationDueDate, EquipmentOperationalStatus.Retired,
            StatusChangeComment: "    \t  \n  ");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(eq.Id, request, 1));
        Assert.Contains("comment explaining the reason for changing the operational status is required", ex.Message);
    }

    [Fact]
    public async Task UpdateMetadata_WithoutStatusChange_DoesNotRequireComment_AndDoesNotRecordHistory()
    {
        await using var db = NewDb();
        var eq = await SeedEquipment(db, EquipmentOperationalStatus.InService);
        var service = new EquipmentInventoryService(db);

        var request = new SaveEquipmentInventoryRequest(
            eq.InstrumentType, "Updated Manufacturer", eq.SerialNumber, "v2.0.0",
            eq.Code, "Updated Room 105", eq.CalibrationDueDate, EquipmentOperationalStatus.InService,
            StatusChangeComment: null);

        await service.UpdateAsync(eq.Id, request, 1);

        var updated = await db.EquipmentInventories.FindAsync(eq.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Manufacturer", updated.ManufacturerName);
        Assert.Equal("v2.0.0", updated.FirmwareVersion);

        var history = await service.GetStatusHistoryAsync(eq.Id);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetStatusHistory_NonExistentEquipment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var service = new EquipmentInventoryService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetStatusHistoryAsync(99999));
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

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.GetContentAsync(doc.Id, eq2.Id, 1));
    }
}
