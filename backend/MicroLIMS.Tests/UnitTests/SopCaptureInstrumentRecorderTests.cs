using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MicroLIMS.Application.Services;
using Xunit;
using static MicroLIMS.Tests.UnitTests.IcpRunSampleTests;

namespace MicroLIMS.Tests.UnitTests;

// The HPLC and ICP "send for review" recorders take their run workspaces from in-memory scenarios that have no
// PostgreSQL counterpart, so their capture point is proved here with a separate context for the assertion. The
// relational behaviour of the capture itself (trigger, lock, audit) is proved in SopRevisionCapturePointsPostgresTests.
public class SopCaptureInstrumentRecorderTests
{
    private static readonly DateTimeOffset CapturedAt = new(2037, 2, 3, 4, 5, 6, TimeSpan.Zero);

    internal static (int MasterId, int RevisionId) LinkSop(MicroLimsDbContext db, int itemId, string code)
    {
        db.Items.Single(i => i.Id == itemId).SopNumber = code;
        var type = new DocumentType { Code = "SOP", Name = "SOP" };
        db.DocumentTypes.Add(type);
        db.SaveChanges();
        var master = new DocumentMaster
        {
            MicroLimsDocumentId = $"ML-{code}", CompanyDocumentCode = code, Title = code, DocumentTypeId = type.Id, DepartmentId = 1,
            SectionId = 1, DocumentOwnerUserId = 1, CreatedByUserId = 1, RecordStatus = DocumentRecordStatus.Active,
        };
        db.DocumentMasters.Add(master);
        db.SaveChanges();
        var revision = new DocumentRevision
        {
            DocumentMasterId = master.Id, RevisionSequence = 3, RevisionNumber = "3", RevisionStatus = DocumentRevisionStatus.Effective,
            CreatedByUserId = 1, EffectiveDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        db.DocumentRevisions.Add(revision);
        db.SaveChanges();
        master.CurrentEffectiveRevisionId = revision.Id;
        db.SaveChanges();
        return (master.Id, revision.Id);
    }

    private static async Task AssertCapturedAsync(MicroLimsDbContext db, int orderId, int masterId, int revisionId, string code)
    {
        var store = db.GetService<IDbContextOptions>().FindExtension<Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal.InMemoryOptionsExtension>()!.StoreName;
        await using var assertDb = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(store).Options);
        var stored = await assertDb.TestOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(SopCaptureStatus.Captured, stored.SopCaptureStatus);
        Assert.Equal(masterId, stored.SopDocumentMasterId);
        Assert.Equal(code, stored.SopDocumentCode);
        Assert.Equal(revisionId, stored.SopRevisionId);
        Assert.Equal("3", stored.SopRevisionNumber);
        Assert.Equal(CapturedAt.UtcDateTime, stored.SopCapturedAtUtc);
    }

    [Fact]
    public async Task Icp_method_assay_submission_captures_the_revision()
    {
        var (f, run, runSampleId) = await AssignedAsync();
        await SaveAsync(f, runSampleId, 0.5m, (f.ZnId, 9.5m));
        await IcpMethodAssayRecorderTests.UploadReportAsync(f, run.Id, runSampleId);
        var db = f.S.Db;
        var (masterId, revisionId) = LinkSop(db, f.Item.Id, "SOP-ICP-1");
        var orderId = (await db.IcpRunSamples.AsNoTracking().SingleAsync(s => s.Id == runSampleId)).TestOrderId;

        await TestServiceFactory.TestWorkflow(db, clock: IcpRunServiceTests.NewClock(),
                sopCapture: new SopRevisionCapture(db, new ItemSopResolver(db), new FixedTimeProvider(CapturedAt)))
            .SubmitIcpMethodAssayAsync(runSampleId, Password, "ok", f.S.UserId, "127.0.0.1");

        await AssertCapturedAsync(db, orderId, masterId, revisionId, "SOP-ICP-1");
    }

    [Fact]
    public async Task Hplc_method_assay_submission_captures_the_revision()
    {
        await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };
        var t = await HplcRunServiceTests.ArrangeSubmittableForSopCaptureAsync(db);
        var itemId = (await db.Samples.AsNoTracking().SingleAsync(s => s.Id == t.Order.SampleId)).ItemId!.Value;
        var (masterId, revisionId) = LinkSop(db, itemId, "SOP-HPLC-1");

        await TestServiceFactory.TestWorkflow(db, clock: t.Clock,
                sopCapture: new SopRevisionCapture(db, new ItemSopResolver(db), new FixedTimeProvider(CapturedAt)))
            .SubmitHplcMethodAssayAsync(t.RunSampleId, "ValidPassword123!", "ok", t.UserId, "127.0.0.1");

        await AssertCapturedAsync(db, t.Order.Id, masterId, revisionId, "SOP-HPLC-1");
    }
}
