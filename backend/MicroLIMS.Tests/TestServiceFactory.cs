using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Tests;

// Keeps archived files in memory so tests never touch the filesystem.
public class InMemoryFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public Task<string> SaveAsync(string fileName, byte[] content)
    {
        Files[fileName] = content;
        return Task.FromResult(fileName);
    }

    public Task<byte[]> ReadAsync(string path) => Task.FromResult(Files[path]);
}

// Builds the real service graph for tests. Centralised so adding a
// dependency to a service does not mean editing every test file that
// constructs it.
public static class TestServiceFactory
{
    public static ReviewGateService ReviewGate(MicroLimsDbContext db) =>
        new(db, new ElectronicSignatureService(db));

    public static RecordArchiveService Archive(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, new PdfGenerator(), storage ?? new InMemoryFileStorageService(), NullLogger<RecordArchiveService>.Instance);

    public static SampleSummaryService SampleSummary(MicroLimsDbContext db) =>
        new(db, new PdfGenerator(), new MicroLIMS.Infrastructure.Word.WordGenerator(), ReviewGate(db));

    public static MediaSummaryService MediaSummary(MicroLimsDbContext db) =>
        new(db, new PdfGenerator(), new MicroLIMS.Infrastructure.Word.WordGenerator(), ReviewGate(db));

    public static CryovialSummaryService CryovialSummary(MicroLimsDbContext db) =>
        new(db, new PdfGenerator(), new MicroLIMS.Infrastructure.Word.WordGenerator(), ReviewGate(db));

    public static SampleReviewService SampleReview(MicroLimsDbContext db) =>
        new(db, new SegregationOfDutiesGuard(db), ReviewGate(db));

    public static ResultProjectionService ResultProjection(MicroLimsDbContext db) =>
        new(db, NullLogger<ResultProjectionService>.Instance);

    public static TestWorkflowEngine TestWorkflow(MicroLimsDbContext db) =>
        new(db, SampleReview(db), ResultProjection(db));

    public static SampleApprovalService SampleApproval(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, ReviewGate(db), SampleSummary(db), Archive(db, storage), ResultProjection(db));

    public static MediaReleaseService MediaRelease(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, new SegregationOfDutiesGuard(db), ReviewGate(db), MediaSummary(db), Archive(db, storage));

    public static CryovialService Cryovial(MicroLimsDbContext db, IFileStorageService? storage = null) =>
        new(db, new MaterialService(db), new SegregationOfDutiesGuard(db), ReviewGate(db),
            CryovialSummary(db), Archive(db, storage));

    public static IncubatorEligibilityService IncubatorEligibility(MicroLimsDbContext db) => new(db);

    public static MediaAppearanceSnapshotService AppearanceSnapshot(MicroLimsDbContext db) =>
        new(db, NullLogger<MediaAppearanceSnapshotService>.Instance);
}
