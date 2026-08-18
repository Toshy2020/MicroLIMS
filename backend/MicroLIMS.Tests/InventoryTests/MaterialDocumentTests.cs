using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Comprehensive tests for the MaterialDocument subsystem.
// Covers: authorization logic, lot isolation, file validation, integrity,
// supersession, void, COA requirement, expiry, and storage failure handling.
public class MaterialDocumentTests
{
    // ---- Test infrastructure ----

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static MaterialDocumentService BuildService(MicroLimsDbContext db, IFileStorageService? storage = null)
    {
        var validator = new MaterialDocumentFileValidator(maxFileSizeBytes: 26_214_400L);
        return new MaterialDocumentService(db, storage ?? new InMemoryFileStorageService(),
            validator, NullLogger<MaterialDocumentService>.Instance);
    }

    private static async Task<Material> SeedMaterial(MicroLimsDbContext db,
        MaterialType type = MaterialType.DehydratedMedia,
        DateTime? expiry = null)
    {
        var material = new Material
        {
            MaterialType = type,
            MaterialName = "Test Material",
            ManufacturerName = "Test Mfg",
            BatchNumber = $"LOT-{Guid.NewGuid():N}".Substring(0, 12),
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = expiry ?? DateTime.UtcNow.AddYears(1),
            Location = "Lab",
            QuantityReceived = 100,
            QuantityRemaining = 100,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        return material;
    }

    private static byte[] MakePdf() =>
        new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4

    private static byte[] MakePng() =>
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header

    private static UploadMaterialDocumentRequest PdfUploadRequest(MaterialDocumentType type = MaterialDocumentType.COA) =>
        new(type, "COA.pdf", "application/pdf", MakePdf());

    // ---- File Validation ----

    [Fact]
    public async Task Upload_PdfFile_Accepted()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical); // non-mandatory; no COA needed to upload
        var service = BuildService(db);

        var doc = await service.UploadAsync(material.Id, PdfUploadRequest(), 1);

        Assert.Equal(MaterialDocumentStatus.Current, doc.Status);
        Assert.Equal(".pdf", doc.FileExtension);
    }

    [Fact]
    public async Task Upload_PngFile_Accepted()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var req = new UploadMaterialDocumentRequest(MaterialDocumentType.SDS, "SDS.png", "image/png", MakePng());
        var doc = await service.UploadAsync(material.Id, req, 1);

        Assert.Equal(".png", doc.FileExtension);
    }

    [Theory]
    [InlineData(".exe", "application/octet-stream")]
    [InlineData(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(".txt", "text/plain")]
    public async Task Upload_InvalidExtension_Rejected(string ext, string mime)
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);
        var content = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var req = new UploadMaterialDocumentRequest(MaterialDocumentType.Other, $"file{ext}", mime, content);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(material.Id, req, 1));
    }

    [Fact]
    public async Task Upload_MimeMismatch_Rejected()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        // .pdf extension but image/jpeg MIME
        var req = new UploadMaterialDocumentRequest(MaterialDocumentType.COA, "test.pdf", "image/jpeg", MakePdf());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(material.Id, req, 1));
    }

    [Fact]
    public async Task Upload_InvalidSignature_Rejected()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        // Correct extension and MIME but wrong magic bytes (JPEG header for a .pdf file)
        var notPdf = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 }; // JPEG SOI
        var req = new UploadMaterialDocumentRequest(MaterialDocumentType.COA, "COA.pdf", "application/pdf", notPdf);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(material.Id, req, 1));
    }

    [Fact]
    public async Task Upload_FileTooLarge_Rejected()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var smallLimit = new MaterialDocumentFileValidator(maxFileSizeBytes: 4);
        var service = new MaterialDocumentService(db, new InMemoryFileStorageService(), smallLimit,
            NullLogger<MaterialDocumentService>.Instance);

        var req = new UploadMaterialDocumentRequest(MaterialDocumentType.COA, "COA.pdf", "application/pdf", MakePdf());
        // MakePdf() is 8 bytes; limit is 4 bytes → should be rejected
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(material.Id, req, 1));
    }

    // ---- SHA-256 Integrity ----

    [Fact]
    public async Task Upload_StoresCorrectSha256()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var content = MakePdf();
        var req = new UploadMaterialDocumentRequest(MaterialDocumentType.COA, "COA.pdf", "application/pdf", content);
        var doc = await service.UploadAsync(material.Id, req, 1);

        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content));
        Assert.Equal(expected, doc.ContentSha256, ignoreCase: true);
    }

    [Fact]
    public async Task GetContent_IntegrityFailure_ThrowsAndDoesNotReturnFile()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var storage = new InMemoryFileStorageService();
        var service = BuildService(db, storage);

        var req = PdfUploadRequest();
        var doc = await service.UploadAsync(material.Id, req, 1);

        // Tamper with the stored file using its StorageKey
        var record = await db.MaterialDocuments.FindAsync(doc.Id);
        storage.Files[record!.StorageKey] = new byte[] { 0x00, 0x00, 0x00, 0x00 };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetContentAsync(doc.Id, material.Id, 1));
    }

    // ---- Lot Isolation ----

    [Fact]
    public async Task GetContent_WrongMaterialId_Throws()
    {
        await using var db = NewDb();
        var mat1 = await SeedMaterial(db, MaterialType.Chemical);
        var mat2 = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var doc = await service.UploadAsync(mat1.Id, PdfUploadRequest(), 1);

        // Attempt to retrieve doc through mat2 → must fail
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetContentAsync(doc.Id, mat2.Id, 1));
    }

    // ---- Supersession ----

    [Fact]
    public async Task Supersede_OldBecomesSuperseded_NewBecomesCurrent()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var old = await service.UploadAsync(material.Id, PdfUploadRequest(), 1);

        var replacement = new SupersedeMaterialDocumentRequest("NewCOA.pdf", "application/pdf", MakePdf(), "Updated version");
        var newDoc = await service.SupersedeAsync(old.Id, material.Id, replacement, 2);

        var reloadedOld = await db.MaterialDocuments.FindAsync(old.Id);
        Assert.Equal(MaterialDocumentStatus.Superseded, reloadedOld!.Status);
        Assert.Equal(2, reloadedOld.SupersededByUserId);
        Assert.Equal("Updated version", reloadedOld.SupersessionReason);
        Assert.Equal(newDoc.Id, reloadedOld.SupersededByDocumentId);

        Assert.Equal(MaterialDocumentStatus.Current, newDoc.Status);
    }

    [Fact]
    public async Task Supersede_OldContentRemainsAccessible()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var oldContent = MakePdf();
        var oldReq = new UploadMaterialDocumentRequest(MaterialDocumentType.COA, "OldCOA.pdf", "application/pdf", oldContent);
        var old = await service.UploadAsync(material.Id, oldReq, 1);

        var replacement = new SupersedeMaterialDocumentRequest("NewCOA.pdf", "application/pdf", MakePdf(), "Reason");
        await service.SupersedeAsync(old.Id, material.Id, replacement, 2);

        // Old document content should still be retrievable
        var (_, retrievedOldContent) = await service.GetContentAsync(old.Id, material.Id, 1);
        Assert.Equal(oldContent, retrievedOldContent);
    }

    [Fact]
    public async Task Supersede_RequiresReason_Throws()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var doc = await service.UploadAsync(material.Id, PdfUploadRequest(), 1);
        var req = new SupersedeMaterialDocumentRequest("NewCOA.pdf", "application/pdf", MakePdf(), "   ");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SupersedeAsync(doc.Id, material.Id, req, 2));
    }

    // ---- Void ----

    [Fact]
    public async Task Void_RequiresReason_Throws()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var doc = await service.UploadAsync(material.Id, PdfUploadRequest(), 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VoidAsync(doc.Id, material.Id, new VoidMaterialDocumentRequest("  "), 2));
    }

    [Fact]
    public async Task Void_DocumentRemainsHistoricallyAccessible()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var doc = await service.UploadAsync(material.Id, PdfUploadRequest(), 1);
        var voided = await service.VoidAsync(doc.Id, material.Id, new VoidMaterialDocumentRequest("Wrong document"), 2);

        Assert.Equal(MaterialDocumentStatus.Voided, voided.Status);
        Assert.Equal("Wrong document", voided.VoidReason);

        // File should still be retrievable
        var (_, content) = await service.GetContentAsync(doc.Id, material.Id, 1);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task Void_VoidedCOA_DoesNotSatisfyCoaRequirement()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.DehydratedMedia);
        var service = BuildService(db);

        var doc = await service.UploadAsync(material.Id, PdfUploadRequest(), 1);
        await service.VoidAsync(doc.Id, material.Id, new VoidMaterialDocumentRequest("Voided"), 2);

        var eligibility = await service.GetCOAEligibilityAsync(material.Id);
        Assert.False(eligibility.HasCurrentCoa);
        Assert.False(eligibility.IsEligible);
    }

    // ---- COA Requirement ----

    [Fact]
    public async Task ConsumeAsync_DehydratedMedia_NoCOA_Blocked()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.DehydratedMedia);
        var materialService = new MaterialService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            materialService.ConsumeAsync(material.Id, MaterialType.DehydratedMedia, 10m, 1));
    }

    [Fact]
    public async Task ConsumeAsync_LyophilizedMicroorganism_NoCOA_Blocked()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.LyophilizedMicroorganism);
        material.Unit = MaterialUnit.Disc;
        await db.SaveChangesAsync();
        var materialService = new MaterialService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            materialService.ConsumeAsync(material.Id, MaterialType.LyophilizedMicroorganism, 1m, 1));
    }

    [Fact]
    public async Task ConsumeAsync_Supplement_NoCOA_Blocked()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Supplement);
        var materialService = new MaterialService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            materialService.ConsumeAsync(material.Id, MaterialType.Supplement, 5m, 1));
    }

    [Fact]
    public async Task ConsumeAsync_DehydratedMedia_WithCurrentCOA_Permitted()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.DehydratedMedia);
        // Seed a current COA
        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = material.Id,
            DocumentType = MaterialDocumentType.COA,
            OriginalFileName = "COA.pdf", StorageKey = "test/coa.pdf",
            FileExtension = ".pdf", ContentType = "application/pdf",
            FileSizeBytes = 100, ContentSha256 = "HASH",
            UploadedByUserId = 1, Status = MaterialDocumentStatus.Current
        });
        await db.SaveChangesAsync();
        var materialService = new MaterialService(db);

        // Should not throw
        await materialService.ConsumeAsync(material.Id, MaterialType.DehydratedMedia, 10m, 1);
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(90m, reloaded!.QuantityRemaining);
    }

    [Fact]
    public async Task ConsumeAsync_WithSupersededOnlyCOA_Blocked()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.DehydratedMedia);
        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = material.Id, DocumentType = MaterialDocumentType.COA,
            OriginalFileName = "OldCOA.pdf", StorageKey = "test/old.pdf",
            FileExtension = ".pdf", ContentType = "application/pdf",
            FileSizeBytes = 100, ContentSha256 = "HASH",
            UploadedByUserId = 1,
            Status = MaterialDocumentStatus.Superseded // not Current
        });
        await db.SaveChangesAsync();
        var materialService = new MaterialService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            materialService.ConsumeAsync(material.Id, MaterialType.DehydratedMedia, 10m, 1));
    }

    [Fact]
    public async Task ConsumeAsync_NonMandatoryMaterial_NoCOA_Permitted()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical); // not mandatory
        var materialService = new MaterialService(db);

        // Should not throw — Chemical does not require a COA
        await materialService.ConsumeAsync(material.Id, MaterialType.Chemical, 5m, 1);
        var reloaded = await db.Materials.FindAsync(material.Id);
        Assert.Equal(95m, reloaded!.QuantityRemaining);
    }

    // ---- Expiry ----

    [Fact]
    public async Task ExpiredMaterial_DocumentsRemainAccessible()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical, expiry: DateTime.UtcNow.AddDays(-1));
        var service = BuildService(db);

        var doc = await service.UploadAsync(material.Id, PdfUploadRequest(), 1);

        // Documents must remain accessible even when the lot is expired
        var (meta, content) = await service.GetContentAsync(doc.Id, material.Id, 1);
        Assert.Equal(doc.Id, meta.Id);
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task ExpiredMaterial_ConsumptionBlocked()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical, expiry: DateTime.UtcNow.AddDays(-1));
        var materialService = new MaterialService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            materialService.ConsumeAsync(material.Id, MaterialType.Chemical, 5m, 1));
    }

    // ---- Storage failure cleanup ----

    [Fact]
    public async Task Upload_StorageFailure_RollsBackDbRecord()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var failingStorage = new FailingFileStorageService();
        var service = BuildService(db, failingStorage);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAsync(material.Id, PdfUploadRequest(), 1));

        // No orphan document record should remain
        var docs = await db.MaterialDocuments.Where(d => d.MaterialId == material.Id).ToListAsync();
        Assert.Empty(docs);
    }

    // ---- COA eligibility helper ----

    [Fact]
    public async Task GetCOAEligibility_NonMandatoryType_ReturnsEligibleFalseRequired()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.Chemical);
        var service = BuildService(db);

        var result = await service.GetCOAEligibilityAsync(material.Id);

        Assert.True(result.IsEligible);
        Assert.False(result.CoaRequired);
    }

    [Fact]
    public async Task GetCOAEligibility_MandatoryTypeWithCurrentCoa_ReturnsEligibleTrue()
    {
        await using var db = NewDb();
        var material = await SeedMaterial(db, MaterialType.DehydratedMedia);
        var service = BuildService(db);

        await service.UploadAsync(material.Id, PdfUploadRequest(), 1);

        var result = await service.GetCOAEligibilityAsync(material.Id);
        Assert.True(result.IsEligible);
        Assert.True(result.CoaRequired);
        Assert.True(result.HasCurrentCoa);
    }

    // ---- LocalFileStorageService nested directory regression tests ----

    [Fact]
    public async Task Upload_PngFile_WithLocalFileStorageService_CreatesNestedDirectoriesAndPersists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "microlims-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = NewDb();
            var material = await SeedMaterial(db, MaterialType.Chemical);
            var localStorage = new LocalFileStorageService(tempDir);
            var service = BuildService(db, localStorage);

            var pngContent = MakePng();
            var req = new UploadMaterialDocumentRequest(MaterialDocumentType.COA, "Screenshot.png", "image/png", pngContent);
            var doc = await service.UploadAsync(material.Id, req, 1);

            Assert.NotNull(doc);
            Assert.Equal(MaterialDocumentStatus.Current, doc.Status);
            Assert.Equal(".png", doc.FileExtension);

            // Verify content retrieval and integrity
            var (meta, content) = await service.GetContentAsync(doc.Id, material.Id, 1);
            Assert.Equal(pngContent, content);

            // Verify physical file was written in nested subdirectory
            var expectedNestedPath = Path.Combine(tempDir, "material-documents", material.Id.ToString(), $"{doc.Id}.png");
            Assert.True(File.Exists(expectedNestedPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Upload_PdfFile_WithLocalFileStorageService_CreatesNestedDirectoriesAndPersists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "microlims-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var db = NewDb();
            var material = await SeedMaterial(db, MaterialType.Chemical);
            var localStorage = new LocalFileStorageService(tempDir);
            var service = BuildService(db, localStorage);

            var pdfContent = MakePdf();
            var req = new UploadMaterialDocumentRequest(MaterialDocumentType.COA, "TestCoa.pdf", "application/pdf", pdfContent);
            var doc = await service.UploadAsync(material.Id, req, 1);

            Assert.NotNull(doc);
            Assert.Equal(MaterialDocumentStatus.Current, doc.Status);
            Assert.Equal(".pdf", doc.FileExtension);

            // Verify content retrieval and integrity
            var (meta, content) = await service.GetContentAsync(doc.Id, material.Id, 1);
            Assert.Equal(pdfContent, content);

            // Verify physical file was written in nested subdirectory
            var expectedNestedPath = Path.Combine(tempDir, "material-documents", material.Id.ToString(), $"{doc.Id}.pdf");
            Assert.True(File.Exists(expectedNestedPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

// Storage stub that always throws on Save so we can test cleanup paths.
public class FailingFileStorageService : MicroLIMS.Infrastructure.Storage.IFileStorageService
{
    public Task<string> SaveAsync(string fileName, byte[] content) =>
        throw new IOException("Simulated storage failure.");

    public Task<byte[]> ReadAsync(string path) =>
        throw new IOException("Simulated storage failure.");
}
