using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

public class ItemDocumentTests
{
    private static (MicroLimsDbContext Db, string TempStorageDir) CreateContext()
    {
        var tempStorageDir = Path.Combine(Path.GetTempPath(), "microlims_test_storage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempStorageDir);

        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        db.Users.Add(new User
        {
            Id = 1,
            FullName = "Ahmed Hamdy",
            Username = "ahamdy",
            PasswordHash = "hash",
            RoleId = 1
        });
        db.SaveChanges();

        return (db, tempStorageDir);
    }

    [Fact]
    public async Task UploadSopDocument_Succeeds_AndReplacesPreviousVersion()
    {
        var (db, tempDir) = CreateContext();
        try
        {
            var item = new Item
            {
                Id = 1,
                Name = "Osteocare Liquid",
                Code = "ost.liq",
                Category = SampleCategory.FinishedProduct,
                SopNumber = "C2I-SOP-001"
            };
            db.Items.Add(item);
            await db.SaveChangesAsync();

            var storage = new LocalFileStorageService(tempDir);
            var service = new ItemDocumentService(db, storage, NullLogger<ItemDocumentService>.Instance);

            // 1. Upload Rev 01 SOP
            using var stream1 = new MemoryStream("SOP content v1"u8.ToArray());
            var doc1 = await service.UploadDocumentAsync(
                itemId: 1,
                documentType: ItemDocumentType.Sop,
                version: "Rev 01",
                effectiveDate: DateTime.UtcNow.AddDays(-30),
                fileStream: stream1,
                originalFileName: "SOP_Osteocare_v1.pdf",
                contentType: "application/pdf",
                fileLength: 14,
                userId: 1);

            Assert.Equal("Rev 01", doc1.Version);
            Assert.Equal(MaterialDocumentStatus.Current, doc1.Status);

            // 2. Upload Rev 02 SOP
            using var stream2 = new MemoryStream("SOP content v2"u8.ToArray());
            var doc2 = await service.UploadDocumentAsync(
                itemId: 1,
                documentType: ItemDocumentType.Sop,
                version: "Rev 02",
                effectiveDate: DateTime.UtcNow,
                fileStream: stream2,
                originalFileName: "SOP_Osteocare_v2.pdf",
                contentType: "application/pdf",
                fileLength: 14,
                userId: 1);

            Assert.Equal("Rev 02", doc2.Version);
            Assert.Equal(MaterialDocumentStatus.Current, doc2.Status);

            // 3. Check list of documents
            var allDocs = await service.GetDocumentsForItemAsync(1);
            Assert.Equal(2, allDocs.Count);

            var supersededDoc = Assert.Single(allDocs, d => d.Id == doc1.Id);
            Assert.Equal(MaterialDocumentStatus.Superseded, supersededDoc.Status);
            Assert.Equal(doc2.Id, supersededDoc.SupersededByDocumentId);

            var currentDoc = Assert.Single(allDocs, d => d.Id == doc2.Id);
            Assert.Equal(MaterialDocumentStatus.Current, currentDoc.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task GetDocumentContent_RecordsAccessLog()
    {
        var (db, tempDir) = CreateContext();
        try
        {
            var item = new Item
            {
                Id = 2,
                Name = "Xanthan Gum",
                Code = "xant.gum",
                Category = SampleCategory.RawMaterial
            };
            db.Items.Add(item);
            await db.SaveChangesAsync();

            var storage = new LocalFileStorageService(tempDir);
            var service = new ItemDocumentService(db, storage, NullLogger<ItemDocumentService>.Instance);

            using var stream = new MemoryStream("Verification report content"u8.ToArray());
            var doc = await service.UploadDocumentAsync(
                itemId: 2,
                documentType: ItemDocumentType.VerificationReport,
                version: "Rev 01",
                effectiveDate: DateTime.UtcNow,
                fileStream: stream,
                originalFileName: "VR_Xanthan.pdf",
                contentType: "application/pdf",
                fileLength: 27,
                userId: 1);

            var (contentStream, contentType, fileName) = await service.GetDocumentContentAsync(doc.Id, 1, isDownload: true);
            Assert.NotNull(contentStream);
            Assert.Equal("application/pdf", contentType);
            Assert.Equal("VR_Xanthan.pdf", fileName);

            var accessLog = await db.ItemDocumentAccessLogs.FirstOrDefaultAsync(l => l.DocumentId == doc.Id);
            Assert.NotNull(accessLog);
            Assert.Equal(MaterialDocumentAccessAction.Download, accessLog.Action);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
