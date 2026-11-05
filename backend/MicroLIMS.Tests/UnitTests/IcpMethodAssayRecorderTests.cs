using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;
using static MicroLIMS.Tests.UnitTests.IcpRunServiceTests;
using static MicroLIMS.Tests.UnitTests.IcpRunSampleTests;

namespace MicroLIMS.Tests.UnitTests;

// ICP Workspace Send for Review (IcpMethodAssayRecorder).
public class IcpMethodAssayRecorderTests
{
    internal static Task UploadReportAsync(Fixture f, int runId, int runSampleId) =>
        f.S.Service.UploadEvidenceAsync(runId, runSampleId, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
            "s.pdf", "application/pdf", new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }), f.S.UserId);

    internal static Task<TestWorkflowResult> SubmitAsync(Fixture f, int runSampleId, string password = Password) =>
        TestServiceFactory.TestWorkflow(f.S.Db, clock: NewClock()).SubmitIcpMethodAssayAsync(runSampleId, password, "ok", f.S.UserId, "127.0.0.1");

    private static async Task<(Fixture F, IcpRunDto Run, int RunSampleId)> EnteredAsync(
        IcpTestMode mode = IcpTestMode.Assay, decimal ppm = 9.5m, bool report = true)
    {
        var (f, run, rsId) = await AssignedAsync(mode);
        await SaveAsync(f, rsId, mode == IcpTestMode.Assay ? 0.5m : null, (f.ZnId, ppm));
        if (report) await UploadReportAsync(f, run.Id, rsId);
        return (f, run, rsId);
    }

    [Fact]
    public async Task Submit_Assay_PersistsRowAndCalculationJson()
    {
        var (f, run, rsId) = await EnteredAsync();

        var result = await SubmitAsync(f, rsId);

        Assert.True(result.AllStepsComplete);
        var db = f.S.Db;
        var analysis = await db.TestAnalyses.Include(a => a.ParameterResults).SingleAsync();
        Assert.True(analysis.IsActive);
        Assert.Equal(WorkflowType.IcpMethodAssay, analysis.AnalysisType);
        Assert.Equal(f.S.Equipment.Id, analysis.EquipmentId);
        var pr = Assert.Single(analysis.ParameterResults);
        Assert.Equal("Zn", pr.ParameterName);
        Assert.Equal("95.0 %", pr.ReportedDisplay);
        Assert.Equal(95m, pr.ReportedValue);
        Assert.Equal("%", pr.Unit);
        Assert.Equal("90-110", pr.SpecLimit);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);
        Assert.Null(pr.ResultBasis);

        using var json = JsonDocument.Parse(pr.CalculationJson!);
        var root = json.RootElement;
        Assert.Equal("Zn", root.GetProperty("element").GetString());
        Assert.Equal("IcpAssayPercent", root.GetProperty("quantity").GetString());
        Assert.Equal("Assay", root.GetProperty("mode").GetString());
        Assert.Equal("Solid", root.GetProperty("sampleForm").GetString());
        Assert.Equal(10m, root.GetProperty("targetPpm").GetDecimal());
        Assert.Equal(0.5m, root.GetProperty("theoreticalAmount").GetDecimal());
        Assert.Equal(0.5m, root.GetProperty("actualWeightG").GetDecimal());
        Assert.Equal(9.5m, root.GetProperty("resultPpm").GetDecimal());
        Assert.Equal(run.Id, root.GetProperty("icpRunId").GetInt32());
        Assert.Equal(rsId, root.GetProperty("icpRunSampleId").GetInt32());
        Assert.Equal(run.Code, root.GetProperty("runCode").GetString());
        Assert.Equal(run.Calibration.Code, root.GetProperty("calibrationCode").GetString());
        Assert.Equal(0.9995m, root.GetProperty("correlationR").GetDecimal());
        Assert.Equal("95.0 %", root.GetProperty("display").GetString());

        Assert.Equal(WorkflowStep.Ready, (await db.TestOrders.SingleAsync()).CurrentStep);
        Assert.NotEmpty(db.ElectronicSignatures.Where(e => e.MeaningOfSignature == SignatureMeaning.ResultRecorded));
    }

    [Fact]
    public async Task Submit_Impurity_StoresPpmAgainstNotMoreThan()
    {
        var (f, _, rsId) = await EnteredAsync(IcpTestMode.Impurity, ppm: 7.25m);

        await SubmitAsync(f, rsId);

        var pr = await f.S.Db.ParameterResults.SingleAsync();
        Assert.Equal("7.25 ppm", pr.ReportedDisplay);
        Assert.Equal("ppm", pr.Unit);
        Assert.Equal("NMT 5", pr.SpecLimit);
        Assert.Equal(ResultStatus.OutOfSpecification, pr.ComparisonStatus);
        using var json = JsonDocument.Parse(pr.CalculationJson!);
        Assert.Equal("IcpImpurityPpm", json.RootElement.GetProperty("quantity").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("targetPpm").ValueKind);
    }

    [Fact]
    public async Task Submit_Impurity_NotDetected_IsWithinLimits()
    {
        var (f, _, rsId) = await EnteredAsync(IcpTestMode.Impurity, ppm: 0m);

        await SubmitAsync(f, rsId);

        var pr = await f.S.Db.ParameterResults.SingleAsync();
        Assert.Equal("0 ppm", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);
    }

    [Fact]
    public async Task Submit_CalibrationExpired_Throws()
    {
        var (f, _, rsId) = await EnteredAsync();
        f.S.Db.IcpCalibrations.Single().ConfirmedAt = SepFirst.UtcDateTime.AddDays(-30);
        await f.S.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, rsId));

        Assert.StartsWith("Calibration expired at", ex.Message);
        Assert.Empty(f.S.Db.TestAnalyses);
    }

    [Fact]
    public async Task Submit_WithoutSampleReport_Throws()
    {
        var (f, _, rsId) = await EnteredAsync(report: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, rsId));
        Assert.Equal("Upload the sample result report before sending for review.", ex.Message);
    }

    [Fact]
    public async Task Submit_WithoutEnteredResults_Throws()
    {
        var (f, run, rsId) = await AssignedAsync();
        await UploadReportAsync(f, run.Id, rsId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, rsId));
        Assert.Equal("Enter the actual weight of the sample.", ex.Message);
    }

    [Fact]
    public async Task Submit_BlockedElement_Throws()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        var run = await ConfirmedRunAsync(s, ("Zn", 0.9980m), ("Ca", 0.9995m)); // Zn fails calibration
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        var rsId = run.Samples.Single().Id;
        await SaveAsync(f, rsId, 0.5m, (f.ZnId, 10m));
        await UploadReportAsync(f, run.Id, rsId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, rsId));

        Assert.StartsWith("Zn is not valid on this run: Failed calibration", ex.Message);
        Assert.Empty(s.Db.TestAnalyses);
    }

    [Fact]
    public async Task Submit_WrongPassword_NothingPersisted()
    {
        var (f, _, rsId) = await EnteredAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => SubmitAsync(f, rsId, "wrong-password"));

        Assert.Empty(f.S.Db.TestAnalyses);
        Assert.Empty(f.S.Db.ParameterResults);
        Assert.Equal(WorkflowStep.Running, (await f.S.Db.TestOrders.SingleAsync()).CurrentStep);
    }

    [Fact]
    public async Task Submit_Twice_Throws()
    {
        var (f, _, rsId) = await EnteredAsync();
        await SubmitAsync(f, rsId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, rsId));
        Assert.Equal("The result has already been sent for review.", ex.Message);
    }

    [Fact]
    public async Task Submit_ThenCompleteRun_Works()
    {
        var (f, run, rsId) = await EnteredAsync();
        await SubmitAsync(f, rsId);

        var done = await f.S.Service.CompleteRunAsync(run.Id, f.S.UserId);

        Assert.Equal(IcpRunStatus.Completed, done.Status);
        var entry = await f.S.Service.GetSampleEntryAsync(rsId, f.S.UserId);
        Assert.True(entry.Submitted);
        Assert.Equal("95.0 %", entry.Official.Single().Display);
        Assert.Equal("IcpAssayPercent", entry.Official.Single().Quantity);
    }

    [Fact]
    public async Task Return_ThenResubmit_Works()
    {
        var (f, run, rsId) = await EnteredAsync();
        await SubmitAsync(f, rsId);
        var db = f.S.Db;
        var role = new Role { Name = "Section Head", Type = RoleType.SectionHead, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var head = new User
        {
            Username = "head_" + Guid.NewGuid().ToString("N")[..6], FullName = "Head", RoleId = role.Id, IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(head);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, head.Id);

        await TestServiceFactory.SampleReview(db).ReturnToAnalystAsync((await db.TestOrders.SingleAsync()).Id, head.Id, "Recheck");

        var entry = await f.S.Service.GetSampleEntryAsync(rsId, f.S.UserId);
        Assert.True(entry.Editable);
        Assert.False(entry.Submitted);
        await SaveAsync(f, rsId, 0.5m, (f.ZnId, 10m));

        await SubmitAsync(f, rsId);

        var analyses = await db.TestAnalyses.Include(x => x.ParameterResults).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, analyses.Count);
        Assert.False(analyses[0].IsActive);
        Assert.True(analyses[1].IsActive);
        Assert.All(analyses[0].ParameterResults, p => Assert.False(p.IsActive));
        Assert.Equal("100.0 %", analyses[1].ParameterResults.Single().ReportedDisplay);
    }
}
