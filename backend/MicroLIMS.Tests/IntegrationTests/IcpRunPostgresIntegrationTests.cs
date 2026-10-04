using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class IcpRunPostgresIntegrationTests
{
    private const string Password = "ValidPassword123!";
    private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D };
    private readonly PostgresTestFixture _fixture;

    public IcpRunPostgresIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task FullRun_RoundTrip()
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst " + tag, Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User
        {
            Username = "icp_" + tag, FullName = "ICP Analyst", RoleId = role.Id, IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        db.CurrentUserId = user.Id;
        var userId = user.Id;

        var calEntry = new MaterialMasterEntry
        {
            SectionId = section.Id, Code = "ICP-CAL-" + tag, Name = "ICP cal " + tag, Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Milliliter, IsActive = true, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = userId, LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(calEntry);
        await db.SaveChangesAsync();
        var lot = new Material
        {
            SectionId = section.Id, MaterialType = MaterialType.ReferenceStandard, MaterialMasterEntryId = calEntry.Id,
            MaterialName = calEntry.Name, ManufacturerName = "Acme", BatchNumber = "LOT-" + tag,
            ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1), Location = "Shelf",
            QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Milliliter,
            Purity = 99.5m, MoisturePercent = 0m, CreatedByUserId = userId, LastModifiedByUserId = userId,
        };
        db.Materials.Add(lot);
        var equipment = new Equipment
        {
            Name = "ICP-OES " + tag, Code = "ICP-" + tag, Type = EquipmentType.IcpOes, SectionId = section.Id,
            CalibrationDueDate = DateTime.UtcNow.AddYears(1),
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var method = await TestServiceFactory.IcpMethod(db).CreateAsync(new SaveIcpMethodRequest(
            "Minerals " + tag, "M" + tag, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), IcpMethodMode.MineralAssay,
            "0.1, 0.5, 1, 3, 6", calEntry.Id, 0.999m, 50m,
            new() { new IcpElementInput(null, "Zn", 213.857m, AnalyteView.Axial), new IcpElementInput(null, "Ca", 317.933m, AnalyteView.Radial) },
            RequireCcv: true, CcvNominalMgPerL: 1m, CcvRecoveryLowPercent: 90m, CcvRecoveryHighPercent: 110m,
            SectionId: section.Id), userId);

        // One ICP test; a Ca item and a Zn item, each with a single-element specification.
        var def = new TestDefinition
        {
            Code = "ICPT_" + tag, DisplayName = "ICP " + tag, SectionId = section.Id,
            WorkflowType = WorkflowType.IcpMethodAssay, EquationType = EquationType.IcpMethodAssay, IcpMethodId = method.Id, IsActive = true,
        };
        var caItem = new Item { Code = "CA-" + tag, Name = "Calcium tablet " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
        var znItem = new Item { Code = "ZN-" + tag, Name = "Zinc tablet " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
        db.TestDefinitions.Add(def);
        db.Items.AddRange(caItem, znItem);
        await db.SaveChangesAsync();
        var elements = await db.IcpMethodElements.Where(e => e.IcpMethodId == method.Id).ToListAsync();
        var znEl = elements.Single(e => e.Symbol == "Zn");
        var caEl = elements.Single(e => e.Symbol == "Ca");
        Specification Spec(Item i, IcpMethodElement el) => new()
        {
            ItemId = i.Id, TestCode = def.Code, ParameterName = el.Symbol + " (amount)", IcpMethodElementId = el.Id,
            ResultBasis = ResultBasis.MgPerUnit, LimitType = LimitType.Range, LowerLimit = 0.9m, UpperLimit = 1.1m,
            LabelClaim = 1m, LabelClaimUnit = "mg", Unit = "mg", DisplayOrder = 1,
        };
        db.Specifications.AddRange(Spec(caItem, caEl), Spec(znItem, znEl));
        await db.SaveChangesAsync();

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync() ?? new CauseOfTesting { Name = "Release", IsActive = true };
        if (cause.Id == 0) db.CausesOfTesting.Add(cause);
        async Task<TestOrder> AddOrderAsync(Item i, string batch)
        {
            var sample = new Sample
            {
                ReferenceNumber = "FP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), ItemId = i.Id,
                Category = SampleCategory.FinishedProduct, BatchNumber = batch, ReceivedAt = DateTime.UtcNow,
                CauseOfTesting = cause, ReceivedByUserId = userId, Status = SampleStatus.InTesting,
            };
            db.Samples.Add(sample);
            await db.SaveChangesAsync();
            var o = new TestOrder
            {
                SampleId = sample.Id, TestCode = def.Code, SectionId = section.Id,
                CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
            };
            db.TestOrders.Add(o);
            await db.SaveChangesAsync();
            return o;
        }
        var caOrder = await AddOrderAsync(caItem, "B-CA");
        var znOrder = await AddOrderAsync(znItem, "B-ZN");

        // Calibration: Ca passes at 0.999386, Zn fails at 0.996383 (min 0.999).
        var svc = TestServiceFactory.IcpRun(db);
        var run = await svc.StartRunAsync(new StartIcpRunRequest(equipment.Id, method.Id), userId);
        run = await svc.SaveCalibrationAsync(run.Id, new SaveIcpCalibrationRequest(lot.Id, null, run.Calibration.Elements.Select(e =>
            new SaveIcpCalibrationElementInput(e.Id, e.Symbol == "Zn" ? 0.996383m : 0.999386m, null, null)).ToList()), userId);
        Assert.True(run.Calibration.Elements.Single(e => e.Symbol == "Ca").Passed);
        Assert.False(run.Calibration.Elements.Single(e => e.Symbol == "Zn").Passed);
        await svc.UploadEvidenceAsync(run.Id, null, IcpEvidenceContext.Calibration, IcpEvidenceKind.CalibrationReport,
            "cal.pdf", "application/pdf", new MemoryStream(Pdf), userId);
        run = await svc.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), userId, null);
        Assert.Equal(IcpCalibrationStatus.Confirmed, run.Calibration.Status);

        run = await svc.AddCcvReadingAsync(run.Id, new AddIcpCcvRequest(caEl.Id, 1.02m), userId);
        Assert.True(run.CcvReadings.Single().Passed);
        Assert.True(run.ElementStates.Single(e => e.Symbol == "Ca").Valid);
        Assert.False(run.ElementStates.Single(e => e.Symbol == "Zn").Valid);

        run = await svc.AssignSamplesAsync(run.Id, new List<int> { caOrder.Id, znOrder.Id }, userId);
        var caRs = run.Samples.Single(x => x.TestOrderId == caOrder.Id).Id;
        var znRs = run.Samples.Single(x => x.TestOrderId == znOrder.Id).Id;

        IcpReplicateInputDto Rep(decimal w, int elementId, decimal c) =>
            new(w, 50m, 10m, new List<IcpConcentrationInput> { new(elementId, c) });
        await svc.SaveReplicatesAsync(caRs, new SaveIcpReplicatesRequest(IcpAmountUnit.Gram, 1.25m,
            new() { Rep(0.5000m, caEl.Id, 0.80m), Rep(0.5100m, caEl.Id, 0.82m) }), userId);
        await svc.UploadEvidenceAsync(run.Id, caRs, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
            "s.pdf", "application/pdf", new MemoryStream(Pdf), userId);
        var workflow = TestServiceFactory.TestWorkflow(db);
        await workflow.SubmitIcpMethodAssayAsync(caRs, Password, "ok", userId, "127.0.0.1");

        await using (var check = _fixture.CreateDbContext())
        {
            var analysis = await check.TestAnalyses.Include(a => a.ParameterResults).SingleAsync(a => a.TestOrderId == caOrder.Id);
            Assert.Equal(WorkflowType.IcpMethodAssay, analysis.AnalysisType);
            Assert.Equal(equipment.Id, analysis.EquipmentId);
            var result = Assert.Single(analysis.ParameterResults);
            Assert.Equal("Ca (amount)", result.ParameterName);
            Assert.NotNull(result.CalculationJson);
            using var json = JsonDocument.Parse(result.CalculationJson!);
            Assert.Equal(run.Id, json.RootElement.GetProperty("icpRunId").GetInt32());
            Assert.Equal("Ca", json.RootElement.GetProperty("element").GetString());
            Assert.Equal(WorkflowStep.Ready, (await check.TestOrders.SingleAsync(o => o.Id == caOrder.Id)).CurrentStep);
        }

        // The Zn order needs Zn, which failed calibration: refused, nothing persisted.
        await svc.SaveReplicatesAsync(znRs, new SaveIcpReplicatesRequest(IcpAmountUnit.Gram, 1.25m,
            new() { Rep(0.5000m, znEl.Id, 0.80m) }), userId);
        await svc.UploadEvidenceAsync(run.Id, znRs, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
            "z.pdf", "application/pdf", new MemoryStream(Pdf), userId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SubmitIcpMethodAssayAsync(znRs, Password, "ok", userId, "127.0.0.1"));
        Assert.StartsWith("Zn is not valid on this run: Failed calibration", ex.Message);
        await using var final = _fixture.CreateDbContext();
        Assert.False(await final.TestAnalyses.AnyAsync(a => a.TestOrderId == znOrder.Id));
    }
}
