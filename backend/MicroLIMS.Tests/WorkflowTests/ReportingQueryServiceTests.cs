using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Covers the Record Search backend additions: the filter-options
// distinct-value endpoint, the export row cap, and the export audit
// trail (DataExportLog) written for every CSV export.
public class ReportingQueryServiceTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // ResultRecord's SampleId/TestOrderId are plain FK-shaped ints - these
    // tests only exercise ReportingQueryService's own filtering/grouping
    // logic, not the Sample/TestOrder join, so one throwaway pair is
    // reused across every seeded row.
    private static async Task<(int sampleId, int testOrderId)> SeedSampleAndOrderAsync(MicroLimsDbContext db)
    {
        var sample = new Sample { Category = SampleCategory.FinishedProduct, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        return (sample.Id, order.Id);
    }

    private static ResultRecord MakeRecord(int sampleId, int testOrderId, string sourceTable, int sourceId,
        SampleCategory category, string testCode, string testDisplayName, string subjectName, string? unit) => new()
    {
        SampleId = sampleId,
        TestOrderId = testOrderId,
        SourceTable = sourceTable,
        SourceId = sourceId,
        ReferenceNumber = "FP0826001",
        Category = category,
        SubjectName = subjectName,
        TestCode = testCode,
        TestDisplayName = testDisplayName,
        ResultKind = unit is null ? ResultKind.Qualitative : ResultKind.Quantitative,
        ReportedValue = unit is null ? "Detected" : "10",
        Unit = unit,
        ResultLevel = ResultLevel.WithinLimit,
        ResultEnteredAt = DateTime.UtcNow,
        ResultEnteredByName = "Analyst",
        SampleStatus = SampleStatus.Received,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetFilterOptionsAsync_ReturnsOnlyDistinctValuesPresentInResultRecords()
    {
        await using var db = NewDb();
        var (sampleId, testOrderId) = await SeedSampleAndOrderAsync(db);

        db.ResultRecords.AddRange(
            MakeRecord(sampleId, testOrderId, "CountTestReading", 1, SampleCategory.FinishedProduct, "TAMC", "Total Aerobic Microbial Count", "Item A", "CFU/g"),
            MakeRecord(sampleId, testOrderId, "CountTestReading", 2, SampleCategory.FinishedProduct, "TAMC", "Total Aerobic Microbial Count", "Item A", "CFU/g"), // duplicate on purpose
            MakeRecord(sampleId, testOrderId, "PathogenObservation", 3, SampleCategory.RawMaterial, "E.coli", "Detection Of E. coli", "Item B", null));
        await db.SaveChangesAsync();

        var query = new ReportingQueryService(db);
        var options = await query.GetFilterOptionsAsync();

        Assert.Equal(new[] { SampleCategory.RawMaterial, SampleCategory.FinishedProduct }.OrderBy(c => c), options.Categories.OrderBy(c => c));
        Assert.Single(options.TestCodes, t => t.TestCode == "TAMC" && t.TestDisplayName == "Total Aerobic Microbial Count");
        Assert.Single(options.TestCodes, t => t.TestCode == "E.coli");
        Assert.Equal(new[] { "Item A", "Item B" }, options.SubjectNames.OrderBy(s => s));
        Assert.Equal(new[] { "CFU/g" }, options.Units); // qualitative row's null Unit never appears
    }

    [Fact]
    public async Task GetForExportAsync_UnderCap_ReturnsAllMatchingRows()
    {
        await using var db = NewDb();
        var (sampleId, testOrderId) = await SeedSampleAndOrderAsync(db);
        db.ResultRecords.AddRange(
            MakeRecord(sampleId, testOrderId, "CountTestReading", 1, SampleCategory.FinishedProduct, "TAMC", "TAMC", "Item A", "CFU/g"),
            MakeRecord(sampleId, testOrderId, "CountTestReading", 2, SampleCategory.FinishedProduct, "TAMC", "TAMC", "Item A", "CFU/g"));
        await db.SaveChangesAsync();

        var query = new ReportingQueryService(db);
        var result = await query.GetForExportAsync(new ResultRecordSearchRequest(null, null, null, null, null, null, null, null, null, null), maxRows: 10);

        Assert.False(result.Exceeded);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    // 10,000-row cap - a too-broad filter must be rejected, not truncated.
    // Uses a small maxRows here to keep the test cheap.
    [Fact]
    public async Task GetForExportAsync_ExceedsCap_ReturnsExceededWithoutItems()
    {
        await using var db = NewDb();
        var (sampleId, testOrderId) = await SeedSampleAndOrderAsync(db);
        db.ResultRecords.AddRange(
            MakeRecord(sampleId, testOrderId, "CountTestReading", 1, SampleCategory.FinishedProduct, "TAMC", "TAMC", "Item A", "CFU/g"),
            MakeRecord(sampleId, testOrderId, "CountTestReading", 2, SampleCategory.FinishedProduct, "TAMC", "TAMC", "Item A", "CFU/g"),
            MakeRecord(sampleId, testOrderId, "CountTestReading", 3, SampleCategory.FinishedProduct, "TAMC", "TAMC", "Item A", "CFU/g"));
        await db.SaveChangesAsync();

        var query = new ReportingQueryService(db);
        var result = await query.GetForExportAsync(new ResultRecordSearchRequest(null, null, null, null, null, null, null, null, null, null), maxRows: 2);

        Assert.True(result.Exceeded);
        Assert.Equal(3, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task LogExportAsync_WritesDataExportLogWithResolvedUserName()
    {
        await using var db = NewDb();
        var role = new Role { Type = RoleType.SystemAdministrator, Name = "System Administrator" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = "Jane Analyst", Username = "jane", RoleId = role.Id, PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var auditService = new DataExportAuditService(db);
        await auditService.LogExportAsync(user.Id, "{\"category\":\"FinishedProduct\"}", rowCount: 42, exportType: "ResultRecordsCsv");

        var log = await db.DataExportLogs.SingleAsync();
        Assert.Equal(user.Id, log.UserId);
        Assert.Equal("Jane Analyst", log.UserName);
        Assert.Equal(42, log.RowCount);
        Assert.Equal("ResultRecordsCsv", log.ExportType);
        Assert.Contains("FinishedProduct", log.FilterJson);
    }

    [Fact]
    public async Task GetOverviewAggregateAsync_CalculatesSqlLevelAggregates()
    {
        await using var db = NewDb();
        var (sampleId, testOrderId) = await SeedSampleAndOrderAsync(db);

        var r1 = MakeRecord(sampleId, testOrderId, "CountTestReading", 1, SampleCategory.FinishedProduct, "TAMC", "TAMC", "Product A", "CFU/g");
        r1.SampleStatus = SampleStatus.Approved;
        r1.ApprovedAt = DateTime.UtcNow;

        var r2 = MakeRecord(sampleId, testOrderId, "CountTestReading", 2, SampleCategory.Water, "TAMC", "TAMC", "Water Point 1", "CFU/mL");
        r2.SampleStatus = SampleStatus.UnderReview;
        r2.ResultLevel = ResultLevel.AlertLevel;

        var r3 = MakeRecord(sampleId, testOrderId, "CountTestReading", 3, SampleCategory.FinishedProduct, "TYMC", "TYMC", "Product A", "CFU/g");
        r3.SampleStatus = SampleStatus.Received;
        r3.ResultLevel = ResultLevel.OutOfSpecification;

        db.ResultRecords.AddRange(r1, r2, r3);
        await db.SaveChangesAsync();

        var query = new ReportingQueryService(db);
        var overview = await query.GetOverviewAggregateAsync(null, null);

        Assert.Equal(3, overview.TotalTests);
        Assert.Equal(1, overview.ApprovedCount);
        Assert.Equal(1, overview.PendingReviewCount);
        Assert.Equal(1, overview.OutOfSpecCount);
        Assert.Equal(1, overview.AlertActionCount);
        Assert.Equal(2, overview.CategoryDistribution.Count);
        Assert.Equal(3, overview.RecentResults.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectRecord()
    {
        await using var db = NewDb();
        var (sampleId, testOrderId) = await SeedSampleAndOrderAsync(db);
        var record = MakeRecord(sampleId, testOrderId, "CountTestReading", 1, SampleCategory.FinishedProduct, "TAMC", "TAMC", "Item A", "CFU/g");
        db.ResultRecords.Add(record);
        await db.SaveChangesAsync();

        var query = new ReportingQueryService(db);
        var found = await query.GetByIdAsync(record.Id);
        var notFound = await query.GetByIdAsync(9999);

        Assert.NotNull(found);
        Assert.Equal(record.Id, found.Id);
        Assert.Equal("TAMC", found.TestCode);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task GetQualitativeEventsAsync_ReturnsDetectedEvents()
    {
        await using var db = NewDb();
        var (sampleId, testOrderId) = await SeedSampleAndOrderAsync(db);

        var r1 = MakeRecord(sampleId, testOrderId, "PathogenObservation", 1, SampleCategory.FinishedProduct, "E.coli", "Detection Of E. coli", "Product X", null);
        r1.ResultKind = ResultKind.Qualitative;
        r1.ReportedValue = "Detected";

        var r2 = MakeRecord(sampleId, testOrderId, "PathogenObservation", 2, SampleCategory.FinishedProduct, "E.coli", "Detection Of E. coli", "Product Y", null);
        r2.ResultKind = ResultKind.Qualitative;
        r2.ReportedValue = "Absent";

        db.ResultRecords.AddRange(r1, r2);
        await db.SaveChangesAsync();

        var query = new ReportingQueryService(db);
        var result = await query.GetQualitativeEventsAsync("E.coli", null, null, null, null);

        Assert.Equal("E.coli", result.TestCode);
        Assert.Single(result.Events);
        Assert.Equal("Product X", result.Events[0].SubjectName);
        Assert.Equal("Detected", result.Events[0].ReportedValue);
    }
}
