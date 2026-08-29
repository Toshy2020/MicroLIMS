using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Validators;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class OosInvestigationDocumentTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static OosInvestigationDocumentService NewService(MicroLimsDbContext db, InMemoryFileStorageService storage)
    {
        var validator = new MaterialDocumentFileValidator(maxFileSizeBytes: 10 * 1024 * 1024);
        return new OosInvestigationDocumentService(db, storage, validator, NullLogger<OosInvestigationDocumentService>.Instance);
    }

    private static byte[] SamplePdfBytes =>
        Encoding.ASCII.GetBytes("%PDF-1.4 Mock PDF Content For Testing %EOF");

    [Fact]
    public async Task UploadAsync_ValidPdf_PersistsDocumentAndStorageKey()
    {
        await using var db = NewDb();
        var storage = new InMemoryFileStorageService();
        var service = NewService(db, storage);

        var oosCode = "OOS0826001";
        db.Samples.Add(new Sample { ReferenceNumber = "S1", ControlNumber = "C1", SampledBy = "A", OosGroupCode = oosCode });
        await db.SaveChangesAsync();

        var request = new UploadOosInvestigationDocumentRequest(
            "Investigation_Report.pdf",
            "application/pdf",
            SamplePdfBytes);

        var doc = await service.UploadAsync(oosCode, request, uploadingUserId: 5);

        Assert.NotNull(doc);
        Assert.Equal(oosCode, doc.OosGroupCode);
        Assert.Equal("Investigation_Report.pdf", doc.OriginalFileName);
        Assert.Equal(MaterialDocumentStatus.Current, doc.Status);
        Assert.Equal(5, doc.UploadedByUserId);

        var stored = await db.OosInvestigationDocuments.SingleAsync();
        Assert.Equal(doc.Id, stored.Id);
        Assert.StartsWith($"oos-investigations/{oosCode}/", stored.StorageKey);
        Assert.True(storage.Files.ContainsKey(stored.StorageKey));
    }

    [Fact]
    public async Task GetDocumentsAsync_NonExistentGroup_Throws()
    {
        await using var db = NewDb();
        var storage = new InMemoryFileStorageService();
        var service = NewService(db, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetDocumentsAsync("OOS9999999"));
    }

    [Fact]
    public async Task GetContentAsync_ValidDocument_ReturnsContentAndVerifiesIntegrity()
    {
        await using var db = NewDb();
        var storage = new InMemoryFileStorageService();
        var service = NewService(db, storage);

        var oosCode = "OOS0826001";
        db.Samples.Add(new Sample { ReferenceNumber = "S1", ControlNumber = "C1", SampledBy = "A", OosGroupCode = oosCode });
        await db.SaveChangesAsync();

        var upload = await service.UploadAsync(oosCode, new UploadOosInvestigationDocumentRequest(
            "Report.pdf", "application/pdf", SamplePdfBytes), uploadingUserId: 1);

        var (meta, bytes) = await service.GetContentAsync(upload.Id, oosCode, requestingUserId: 2);
        Assert.Equal(upload.Id, meta.Id);
        Assert.Equal(SamplePdfBytes, bytes);
    }

    [Fact]
    public async Task SupersedeAsync_ReplacesCurrentDocumentWithNewOne()
    {
        await using var db = NewDb();
        var storage = new InMemoryFileStorageService();
        var service = NewService(db, storage);

        var oosCode = "OOS0826001";
        db.Samples.Add(new Sample { ReferenceNumber = "S1", ControlNumber = "C1", SampledBy = "A", OosGroupCode = oosCode });
        await db.SaveChangesAsync();

        var initial = await service.UploadAsync(oosCode, new UploadOosInvestigationDocumentRequest(
            "Report_v1.pdf", "application/pdf", SamplePdfBytes), uploadingUserId: 1);

        var newPdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 Mock Replacement PDF Content %EOF");
        var superseded = await service.SupersedeAsync(initial.Id, oosCode, new SupersedeOosInvestigationDocumentRequest(
            "Report_v2.pdf", "application/pdf", newPdfBytes, "Updated with root cause analysis"), actingUserId: 2);

        Assert.NotEqual(initial.Id, superseded.Id);
        Assert.Equal(MaterialDocumentStatus.Current, superseded.Status);
        Assert.Equal("Report_v2.pdf", superseded.OriginalFileName);

        var oldDoc = await db.OosInvestigationDocuments.FirstAsync(d => d.Id == initial.Id);
        Assert.Equal(MaterialDocumentStatus.Superseded, oldDoc.Status);
        Assert.Equal(superseded.Id, oldDoc.SupersededByDocumentId);
        Assert.Equal("Updated with root cause analysis", oldDoc.SupersessionReason);
        Assert.Equal(2, oldDoc.SupersededByUserId);
    }

    [Fact]
    public async Task VoidAsync_MarksDocumentVoided()
    {
        await using var db = NewDb();
        var storage = new InMemoryFileStorageService();
        var service = NewService(db, storage);

        var oosCode = "OOS0826001";
        db.Samples.Add(new Sample { ReferenceNumber = "S1", ControlNumber = "C1", SampledBy = "A", OosGroupCode = oosCode });
        await db.SaveChangesAsync();

        var upload = await service.UploadAsync(oosCode, new UploadOosInvestigationDocumentRequest(
            "Report.pdf", "application/pdf", SamplePdfBytes), uploadingUserId: 1);

        var voided = await service.VoidAsync(upload.Id, oosCode, new VoidOosInvestigationDocumentRequest(
            "Uploaded against wrong investigation"), actingUserId: 3);

        Assert.Equal(MaterialDocumentStatus.Voided, voided.Status);
        Assert.Equal("Uploaded against wrong investigation", voided.VoidReason);
        Assert.Equal(3, voided.VoidedByUserId);
    }
}
