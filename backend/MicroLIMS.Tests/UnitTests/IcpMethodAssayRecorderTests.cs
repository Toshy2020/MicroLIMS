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
    private static Task UploadReportAsync(Fixture f, int runId, int runSampleId) =>
        f.S.Service.UploadEvidenceAsync(runId, runSampleId, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
            "s.pdf", "application/pdf", new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }), f.S.UserId);

    private static Task<TestWorkflowResult> SubmitAsync(Fixture f, int runSampleId, string password = Password) =>
        TestServiceFactory.TestWorkflow(f.S.Db, clock: NewClock()).SubmitIcpMethodAssayAsync(runSampleId, password, "ok", f.S.UserId, "127.0.0.1");

    private static async Task<(Fixture F, IcpRunDto Run, int RunSampleId)> EnteredAsync(bool report = true, decimal zn2 = 0.82m)
    {
        var (f, run, rsId) = await AssignedAsync();
        await SaveAsync(f, rsId, Rep(f, 0.5000m, 0.80m), Rep(f, 0.5100m, zn2));
        if (report) await UploadReportAsync(f, run.Id, rsId);
        return (f, run, rsId);
    }

    [Fact]
    public async Task Submit_Mineral_PersistsRowsAndCalculationJson()
    {
        var (f, run, rsId) = await EnteredAsync();

        var result = await SubmitAsync(f, rsId);

        Assert.True(result.AllStepsComplete);
        var db = f.S.Db;
        var analysis = await db.TestAnalyses.Include(a => a.ParameterResults).SingleAsync();
        Assert.True(analysis.IsActive);
        Assert.Equal(WorkflowType.IcpMethodAssay, analysis.AnalysisType);
        Assert.Equal(f.S.Equipment.Id, analysis.EquipmentId);
        Assert.Equal(2, analysis.ParameterResults.Count);
        var amount = analysis.ParameterResults.Single(p => p.ParameterName == "Zn (amount)");
        Assert.Equal("1.002 mg", amount.ReportedDisplay);
        Assert.Equal(1.002451m, Math.Round(amount.ReportedValue!.Value, 6));
        Assert.Equal(ResultStatus.WithinLimits, amount.ComparisonStatus);
        Assert.Equal("mg", amount.Unit);
        Assert.Equal(ResultBasis.MgPerUnit, amount.ResultBasis);
        Assert.NotNull(amount.SpecificationId);
        var pct = analysis.ParameterResults.Single(p => p.ParameterName == "Zn (%LC)");
        Assert.Equal("100.2 %", pct.ReportedDisplay);

        using var json = JsonDocument.Parse(amount.CalculationJson!);
        var root = json.RootElement;
        Assert.Equal("Zn", root.GetProperty("element").GetString());
        Assert.Equal("IcpMgPerUnit", root.GetProperty("quantity").GetString());
        Assert.Equal("IcpPercentLabelClaim", JsonDocument.Parse(pct.CalculationJson!).RootElement.GetProperty("quantity").GetString());
        Assert.Equal(1.25m, root.GetProperty("unitAmount").GetDecimal());
        Assert.Equal(2, root.GetProperty("replicates").GetArrayLength());
        Assert.Equal(1, root.GetProperty("replicates")[0].GetProperty("replicateNo").GetInt32());
        Assert.Equal(run.Id, root.GetProperty("icpRunId").GetInt32());
        Assert.Equal(rsId, root.GetProperty("icpRunSampleId").GetInt32());
        Assert.Equal(run.Code, root.GetProperty("runCode").GetString());
        Assert.Equal(0.9995m, root.GetProperty("correlationR").GetDecimal());
        Assert.Equal(5, root.GetProperty("standardLevelsMgPerL").GetArrayLength());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("ccv").ValueKind);
        Assert.Equal("1.002 mg", root.GetProperty("display").GetString());

        Assert.Equal(WorkflowStep.Ready, (await db.TestOrders.SingleAsync()).CurrentStep);
        Assert.NotEmpty(db.ElectronicSignatures.Where(e => e.MeaningOfSignature == SignatureMeaning.ResultRecorded));
    }

    [Fact]
    public async Task Submit_AllBelowLoq_StoresLoqDisplay()
    {
        var (f, run, rsId) = await AssignedAsync(znBases: false, znLimit: LimitType.NotMoreThan);
        await SaveAsync(f, rsId, Rep(f, 0.5m, 0.05m), Rep(f, 0.5m, 0.09m));
        await UploadReportAsync(f, run.Id, rsId);

        await SubmitAsync(f, rsId);

        var pr = await f.S.Db.ParameterResults.SingleAsync();
        Assert.Equal("<LOQ", pr.ReportedDisplay);
        Assert.Null(pr.ReportedValue);
        Assert.True(pr.BelowLoq);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);
    }

    [Fact]
    public async Task Submit_BlockedElement_Throws()
    {
        var s = await SeedAsync();
        var f = await SeedFixtureAsync(s);
        var order = await AddOrderAsync(f);
        var run = await ConfirmedRunAsync(s, znR: 0.9980m); // Zn fails calibration
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { order.Id }, s.UserId);
        var rsId = run.Samples.Single().Id;
        await SaveAsync(f, rsId, Rep(f, 0.5m, 0.8m));
        await UploadReportAsync(f, run.Id, rsId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, rsId));

        Assert.StartsWith("Zn is not valid on this run: Failed calibration", ex.Message);
        Assert.Empty(s.Db.TestAnalyses);
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
    public async Task Submit_OverRange_Throws()
    {
        var (f, _, rsId) = await EnteredAsync(zn2: 6.5m);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, rsId));
        Assert.Equal("Zn: replicate 2 is above the top standard - dilute and re-measure.", ex.Message);
        Assert.Empty(f.S.Db.TestAnalyses);
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

    // A failing CCV blocks the element for samples not yet sent for review; sent results are untouched.
    [Fact]
    public async Task FailingCcv_LocksElement_OnlyForUnsubmitted()
    {
        var s = await SeedAsync(requireCcv: true);
        var f = await SeedFixtureAsync(s, znBases: false);
        s.Db.Specifications.Add(new Specification
        {
            ItemId = f.Item.Id, TestCode = f.Definition.Code, ParameterName = "Ca (impurity)", IcpMethodElementId = f.CaId,
            ResultBasis = ResultBasis.MgPerKg, LimitType = LimitType.Range, LowerLimit = 0m, UpperLimit = 1000m, Unit = "µg/g", DisplayOrder = 2,
        });
        await s.Db.SaveChangesAsync();
        var orderA = await AddOrderAsync(f, "B-A");
        var orderB = await AddOrderAsync(f, "B-B");
        var run = await ConfirmedRunAsync(s);
        await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(f.ZnId, 1.0m), s.UserId);
        await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(f.CaId, 1.0m), s.UserId);
        run = await s.Service.AssignSamplesAsync(run.Id, new List<int> { orderA.Id, orderB.Id }, s.UserId);
        var (a, b) = (run.Samples.Single(x => x.TestOrderId == orderA.Id).Id, run.Samples.Single(x => x.TestOrderId == orderB.Id).Id);

        IcpReplicateInputDto Both() => new(0.5m, 50m, 10m, new List<IcpConcentrationInput> { new(f.ZnId, 0.8m), new(f.CaId, 0.8m) });
        foreach (var id in new[] { a, b })
        {
            await s.Service.SaveReplicatesAsync(id, new SaveIcpReplicatesRequest(IcpAmountUnit.Gram, null, new() { Both() }), s.UserId);
            await UploadReportAsync(f, run.Id, id);
        }

        await SubmitAsync(f, a);
        await s.Service.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(f.CaId, 0.5m), s.UserId); // fails

        var analysis = await s.Db.TestAnalyses.Include(x => x.ParameterResults).SingleAsync(x => x.TestOrderId == orderA.Id);
        Assert.True(analysis.IsActive);
        Assert.Equal(2, analysis.ParameterResults.Count);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(f, b));
        Assert.StartsWith("Ca is not valid on this run: CCV failed", ex.Message);
        Assert.False(await s.Db.TestAnalyses.AnyAsync(x => x.TestOrderId == orderB.Id));
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

        await TestServiceFactory.Review(db).ReturnToAnalystAsync((await db.TestOrders.SingleAsync()).Id, head.Id, "Recheck");

        var entry = await f.S.Service.GetSampleEntryAsync(rsId, f.S.UserId);
        Assert.True(entry.Editable);
        Assert.False(entry.Submitted);
        await SaveAsync(f, rsId, Rep(f, 0.5000m, 0.80m), Rep(f, 0.5100m, 0.80m));

        await SubmitAsync(f, rsId);

        var analyses = await db.TestAnalyses.Include(x => x.ParameterResults).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, analyses.Count);
        Assert.False(analyses[0].IsActive);
        Assert.True(analyses[1].IsActive);
        Assert.All(analyses[0].ParameterResults, p => Assert.False(p.IsActive));
    }
}
