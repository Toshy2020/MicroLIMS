using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class AfterCleaningReceivingTests
{
    private class TestPdfGenerator : IPdfGenerator
    {
        public List<string> CapturedLines { get; } = new();

        public Task<byte[]> GenerateAsync(string templateName, Dictionary<string, object> data) =>
            Task.FromResult(new byte[] { 1, 2, 3 });

        public Task<byte[]> GenerateFromLinesAsync(string title, IEnumerable<string> lines)
        {
            CapturedLines.AddRange(lines);
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        public Task<byte[]> GenerateReportAsync(ReportDocument document) =>
            Task.FromResult(new byte[] { 1, 2, 3 });
    }

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task ReceiveAsync_MissingPreviousProductName_Throws()
    {
        using var db = NewDb();
        var machine = new Machine { Name = "Blender 01" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen);

        var req = new AfterCleaningReceiveRequest(
            MachineId: machine.Id,
            CauseOfTestingId: 1,
            SampledBy: "Analyst 1",
            ControlNumber: "AC-CTRL-01",
            ReceivedByUserId: 1,
            PreviousProductName: "   ",
            PreviousProductBatchNumber: "PCT-260824"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(req));
        Assert.Contains("Previous Product is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceiveAsync_MissingPreviousProductBatchNumber_Throws()
    {
        using var db = NewDb();
        var machine = new Machine { Name = "Blender 01" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen);

        var req = new AfterCleaningReceiveRequest(
            MachineId: machine.Id,
            CauseOfTestingId: 1,
            SampledBy: "Analyst 1",
            ControlNumber: "AC-CTRL-01",
            ReceivedByUserId: 1,
            PreviousProductName: "Paracetamol 500 mg Tablets",
            PreviousProductBatchNumber: "   "
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ReceiveAsync(req));
        Assert.Contains("Previous Product Batch Number is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReceiveAsync_ValidArbitraryPreviousProductAndBatch_PersistsCorrectlyWithoutItemId()
    {
        using var db = NewDb();
        var machine = new Machine { Name = "Compression Machine CTX-02" };
        var cause = new CauseOfTesting { Name = "Routine" };
        db.Machines.Add(machine);
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen);

        // Product not in Item master (e.g. historical/obsolete product name)
        string customProductName = "Discontinued Amoxicillin 250mg Suspension (Legacy Form)";
        string customBatchNumber = "AMX-LEGACY-998";

        var req = new AfterCleaningReceiveRequest(
            MachineId: machine.Id,
            CauseOfTestingId: cause.Id,
            SampledBy: "Ahmed Reda",
            ControlNumber: "AC-260825-014",
            ReceivedByUserId: 1,
            PreviousProductName: customProductName,
            PreviousProductBatchNumber: customBatchNumber
        );

        var sample = await engine.ReceiveAsync(req);

        Assert.NotNull(sample);
        Assert.Equal(SampleCategory.AfterCleaning, sample.Category);
        Assert.Equal(machine.Id, sample.MachineId);
        Assert.Null(sample.ItemId); // Must NOT assign ItemId
        Assert.Equal(customProductName, sample.PreviousProductName);
        Assert.Equal(customBatchNumber, sample.PreviousProductBatchNumber);
        Assert.Equal(customBatchNumber, sample.BatchNumber);
        Assert.Equal("AC-260825-014", sample.ControlNumber);

        // Verify ToDto maps correctly
        var dto = TestingWorkspaceService.ToDto(sample);
        Assert.Equal(machine.Name, dto.DisplayName);
        Assert.Null(dto.ItemId);
        Assert.Equal(customProductName, dto.PreviousProductName);
        Assert.Equal(customBatchNumber, dto.PreviousProductBatchNumber);
    }

    [Fact]
    public async Task GenerateAfterCleaningReportPdfAsync_IncludesPreviousProductAndBatch()
    {
        using var db = NewDb();
        var machine = new Machine { Name = "Blender 01" };
        var cause = new CauseOfTesting { Name = "Routine" };
        db.Machines.Add(machine);
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "AC0826001",
            Category = SampleCategory.AfterCleaning,
            MachineId = machine.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Analyst 1",
            ControlNumber = "AC-CTRL-01",
            ReceivedByUserId = 1,
            PreviousProductName = "Ibuprofen 400 mg Tablets",
            PreviousProductBatchNumber = "IBU-260825",
            BatchNumber = "IBU-260825"
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var testPdf = new TestPdfGenerator();
        var reportService = new ReportService(testPdf, db);
        var bytes = await reportService.GenerateAfterCleaningReportPdfAsync(sample.Id);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        Assert.Contains(testPdf.CapturedLines, l => l.Contains("Previous Product: Ibuprofen 400 mg Tablets") && l.Contains("Previous Batch: IBU-260825"));
    }
}
