using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class AfterCleaningPhase4bTests
{
    private const string Password = "Password123!";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed record TestSetup(
        DocumentSection Section,
        User User,
        Machine Machine,
        MachinePart Part1,
        MachinePart Part2,
        Item FinishedProduct1,
        Item FinishedProduct2,
        Item RawMaterial,
        SolutionMaster Diluent,
        SolutionMaster MobilePhase,
        MaterialMasterEntry StandardEntry,
        Equipment Equipment,
        ChromatographyColumn Column);

    private static TestSetup Seed(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "QC", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var fpSection = new DocumentSection
        {
            Name = "Physicochemical Laboratory",
            Code = PhyschemAreas.SectionCode,
            DepartmentId = dept.Id,
            IsActive = true
        };
        db.DocumentSections.Add(fpSection);

        var adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        db.Roles.Add(adminRole);
        db.SaveChanges();

        var user = new User
        {
            Username = "chemist",
            FullName = "Chemist Analyst",
            RoleId = adminRole.Id,
            Role = adminRole,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = dept.Id,
            SectionId = fpSection.Id,
            PhyschemArea = PhyschemArea.FinishedProduct
        });

        var machine = new Machine { Name = "Granulator 01", IsActive = true };
        db.Machines.Add(machine);
        db.SaveChanges();

        var part1 = new MachinePart { MachineId = machine.Id, Name = "Impeller", IsActive = true };
        var part2 = new MachinePart { MachineId = machine.Id, Name = "Chopper", IsActive = true };
        db.MachineParts.AddRange(part1, part2);

        var fp1 = new Item { Name = "Paracetamol 500mg Tablets", Code = "FP-001", Category = SampleCategory.FinishedProduct, IsActive = true };
        var fp2 = new Item { Name = "Ibuprofen 400mg Tablets", Code = "FP-002", Category = SampleCategory.FinishedProduct, IsActive = true };
        var rm = new Item { Name = "Starch BP", Code = "RM-001", Category = SampleCategory.RawMaterial, IsActive = true };
        db.Items.AddRange(fp1, fp2, rm);

        var diluent = new SolutionMaster
        {
            SectionId = fpSection.Id,
            Name = "Diluent Water",
            Type = SolutionType.Diluent,
            ShelfLifeValue = 7,
            ShelfLifeUnit = ShelfLifeUnit.Days,
            StorageCondition = "RT",
            FinalVolumeMl = 1000m,
            Instructions = "Filter",
            IsActive = true
        };
        var mp = new SolutionMaster
        {
            SectionId = fpSection.Id,
            Name = "Mobile Phase A",
            Type = SolutionType.MobilePhase,
            ShelfLifeValue = 7,
            ShelfLifeUnit = ShelfLifeUnit.Days,
            StorageCondition = "RT",
            FinalVolumeMl = 1000m,
            Instructions = "Mix",
            IsActive = true
        };
        db.SolutionMasters.AddRange(diluent, mp);

        var standard = new MaterialMasterEntry
        {
            SectionId = fpSection.Id,
            Code = "STD-PARA",
            Name = "Paracetamol Reference Standard",
            Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Gram,
            IsActive = true
        };
        db.MaterialMasterEntries.Add(standard);

        var equipment = new Equipment
        {
            SectionId = fpSection.Id,
            Code = "EQ-001",
            Name = "HPLC Agilent 1260",
            Type = EquipmentType.Hplc
        };
        db.Equipment.Add(equipment);

        var column = new ChromatographyColumn
        {
            SectionId = fpSection.Id,
            Code = "COL-001",
            Name = "C18 Column",
            SerialNumber = "SN-12345",
            IsActive = true
        };
        db.ChromatographyColumns.Add(column);
        db.SaveChanges();

        return new TestSetup(fpSection, user, machine, part1, part2, fp1, fp2, rm, diluent, mp, standard, equipment, column);
    }

    private static SaveHplcMethodRequest CreateMethodRequest(
        TestSetup s,
        string abbreviation,
        int? productItemId,
        HplcTechnique technique = HplcTechnique.Hplc,
        HplcResultMode resultMode = HplcResultMode.CleaningResidue,
        decimal? sampleSolutionVolumeMl = 10m,
        decimal? swabAreaCm2 = 25m,
        decimal? standardConc = 5m,
        decimal? macoLimit = 10m,
        MacoUnit? macoUnit = MacoUnit.UgPerSwab,
        decimal theoreticalWeightStdMg = 0m,
        decimal theoreticalWeightTestMg = 0m,
        decimal? filmThicknessUm = null,
        CarrierGas? carrierGas = null,
        decimal? inletTemp = null,
        decimal? detectorTemp = null)
    {
        return new SaveHplcMethodRequest(
            Name: "Method " + abbreviation,
            Abbreviation: abbreviation,
            EffectiveDate: DateTime.UtcNow,
            ColumnDesignation: "L1",
            ColumnLengthMm: 150m,
            ColumnInternalDiameterMm: 4.6m,
            ParticleSizeUm: 5m,
            ColumnTemperatureC: 30m,
            ElutionMode: ElutionMode.Isocratic,
            FlowRateMlPerMin: 1m,
            DetectorType: HplcDetectorType.UV,
            InjectionVolumeUl: 20m,
            RunTimeMin: 15m,
            DiluentSolutionId: s.Diluent.Id,
            MobilePhases: new List<HplcMobilePhaseInput> { new("A", s.MobilePhase.Id, null) },
            GradientSteps: new(),
            Analytes: new List<HplcAnalyteInput>
            {
                new(
                    Id: null,
                    Name: "Paracetamol",
                    WavelengthNm: 254m,
                    StandardEntryId: s.StandardEntry.Id,
                    TheoreticalWeightStdMg: theoreticalWeightStdMg,
                    TheoreticalWeightTestMg: theoreticalWeightTestMg,
                    StandardInjections: 5,
                    StandardConcentrationUgPerMl: standardConc,
                    MacoLimit: macoLimit,
                    MacoUnit: macoUnit)
            },
            SectionId: s.Section.Id,
            Technique: technique,
            ResultMode: resultMode,
            SampleSolutionVolumeMl: sampleSolutionVolumeMl,
            ProductItemId: productItemId,
            SwabAreaCm2: swabAreaCm2,
            FilmThicknessUm: filmThicknessUm,
            CarrierGas: carrierGas,
            InletTemperatureC: inletTemp,
            DetectorTemperatureC: detectorTemp);
    }

    [Fact]
    public async Task HplcMethodValidation_CleaningResidue_EnforcesRules()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var svc = TestServiceFactory.HplcMethod(db);

        // 1. CleaningResidue requires FinishedProduct ProductItemId
        var reqNoItem = CreateMethodRequest(s, "M-NO-ITEM", productItemId: null);
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqNoItem, s.User.Id));
        Assert.Contains("finished product item is required", ex1.Message);

        // 2. Reject RawMaterial item
        var reqRmItem = CreateMethodRequest(s, "M-RM-ITEM", productItemId: s.RawMaterial.Id);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqRmItem, s.User.Id));
        Assert.Contains("Finished Product item", ex2.Message);

        // 3. Extraction volume (SampleSolutionVolumeMl) must be > 0
        var reqNoVol = CreateMethodRequest(s, "M-NO-VOL", productItemId: s.FinishedProduct1.Id, sampleSolutionVolumeMl: 0m);
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqNoVol, s.User.Id));
        Assert.Contains("Sample extraction volume (mL) must be greater than zero", ex3.Message);

        // 4. Each analyte requires StandardConcentrationUgPerMl > 0
        var reqNoStdConc = CreateMethodRequest(s, "M-NO-STD", productItemId: s.FinishedProduct1.Id, standardConc: 0m);
        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqNoStdConc, s.User.Id));
        Assert.Contains("standard concentration (µg/mL) is required", ex4.Message);

        // 5. Each analyte requires MacoLimit > 0
        var reqNoMaco = CreateMethodRequest(s, "M-NO-MACO", productItemId: s.FinishedProduct1.Id, macoLimit: 0m);
        var ex5 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqNoMaco, s.User.Id));
        Assert.Contains("MACO limit must be greater than zero", ex5.Message);

        // 6. Each analyte requires MacoUnit
        var reqNoUnit = CreateMethodRequest(s, "M-NO-UNIT", productItemId: s.FinishedProduct1.Id, macoUnit: null);
        var ex6 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqNoUnit, s.User.Id));
        Assert.Contains("MACO unit is required", ex6.Message);

        // 7. When UgPerCm2, SwabAreaCm2 must be > 0
        var reqNoArea = CreateMethodRequest(s, "M-NO-AREA", productItemId: s.FinishedProduct1.Id, macoUnit: MacoUnit.UgPerCm2, swabAreaCm2: 0m);
        var ex7 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqNoArea, s.User.Id));
        Assert.Contains("Swabbed area (cm²) must be greater than zero", ex7.Message);

        // 8. GC rejects CleaningResidue
        var reqGc = CreateMethodRequest(s, "M-GC-CR", productItemId: s.FinishedProduct1.Id, technique: HplcTechnique.Gc);
        var ex8 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqGc, s.User.Id));
        Assert.Contains("Cleaning residue is available for HPLC methods only", ex8.Message);

        // 9. Other modes reject cleaning residue fields
        var reqAssayWithCr = CreateMethodRequest(s, "M-ASSAY-CR", productItemId: s.FinishedProduct1.Id, resultMode: HplcResultMode.Assay, theoreticalWeightStdMg: 50m, theoreticalWeightTestMg: 50m);
        var ex9 = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(reqAssayWithCr, s.User.Id));
        Assert.Contains("Cleaning residue settings belong to cleaning residue methods only", ex9.Message);

        // 10. Valid CleaningResidue method succeeds
        var validReq = CreateMethodRequest(s, "M-VALID-CR", productItemId: s.FinishedProduct1.Id, swabAreaCm2: 25m, macoLimit: 2.5m, macoUnit: MacoUnit.UgPerCm2);
        var created = await svc.CreateAsync(validReq, s.User.Id);
        Assert.Equal(s.FinishedProduct1.Id, created.ProductItemId);
        Assert.Equal(s.FinishedProduct1.Name, created.ProductItemName);
        Assert.Equal(25m, created.SwabAreaCm2);
        Assert.Equal(2.5m, created.Analytes[0].MacoLimit);
        Assert.Equal(MacoUnit.UgPerCm2, created.Analytes[0].MacoUnit);
    }

    [Fact]
    public async Task IcpTestDefinitionRules_ValidatesCleaningLimit()
    {
        await using var db = NewDb();
        var entity = new TestDefinition
        {
            Code = "ICP_CLEAN",
            DisplayName = "ICP Cleaning Test",
            SectionId = 1,
            WorkflowType = WorkflowType.IcpMethodAssay,
            IsActive = true
        };
        db.TestDefinitions.Add(entity);
        await db.SaveChangesAsync();

        var inputNegative = new IcpTestElementInput(null, "Fe", 10m, CleaningLimitPpm: -1m);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            IcpTestDefinitionRules.ApplyAsync(db, entity, true, IcpTestMode.Assay, IcpSampleForm.Solid, 1m, null, new() { inputNegative }));
        Assert.Contains("cleaning limit (ppm) cannot be negative", ex.Message);

        var inputValid = new IcpTestElementInput(null, "Fe", 10m, CleaningLimitPpm: 5m);
        await IcpTestDefinitionRules.ApplyAsync(db, entity, true, IcpTestMode.Assay, IcpSampleForm.Solid, 1m, null, new() { inputValid });
        Assert.Single(entity.IcpElements);
        Assert.Equal(5m, entity.IcpElements.First().CleaningLimitPpm);
    }

    [Fact]
    public async Task AfterCleaningPreparation_OneOrderPerConfigForHplcAndIcp_RefusesWrongProduct()
    {
        await using var db = NewDb();
        var s = Seed(db);
        var scope = new UserSectionScopeService(db);
        var refGen = new ReferenceNumberGenerator(db);
        var engine = new AfterCleaningWorkflowEngine(db, refGen, scope);

        // Create HPLC Cleaning Method for FP1
        var hplcMethodFp1 = new HplcMethod
        {
            SectionId = s.Section.Id,
            Abbreviation = "M-HPLC-FP1",
            Name = "Paracetamol Cleaning HPLC",
            Technique = HplcTechnique.Hplc,
            ResultMode = HplcResultMode.CleaningResidue,
            SampleSolutionVolumeMl = 10m,
            ProductItemId = s.FinishedProduct1.Id,
            Analytes = new()
            {
                new() { Name = "Paracetamol", DisplayOrder = 1, StandardConcentrationUgPerMl = 5m, MacoLimit = 50m, MacoUnit = MacoUnit.UgPerSwab }
            }
        };
        db.HplcMethods.Add(hplcMethodFp1);

        // HPLC Cleaning Method for FP2
        var hplcMethodFp2 = new HplcMethod
        {
            SectionId = s.Section.Id,
            Abbreviation = "M-HPLC-FP2",
            Name = "Ibuprofen Cleaning HPLC",
            Technique = HplcTechnique.Hplc,
            ResultMode = HplcResultMode.CleaningResidue,
            SampleSolutionVolumeMl = 10m,
            ProductItemId = s.FinishedProduct2.Id,
            Analytes = new()
            {
                new() { Name = "Ibuprofen", DisplayOrder = 1, StandardConcentrationUgPerMl = 5m, MacoLimit = 50m, MacoUnit = MacoUnit.UgPerSwab }
            }
        };
        db.HplcMethods.Add(hplcMethodFp2);

        // Test Definitions
        var hplcTestFp1 = new TestDefinition
        {
            Code = "HPLC_AC",
            DisplayName = "HPLC Cleaning Swab",
            WorkflowType = WorkflowType.HplcMethodAssay,
            SectionId = s.Section.Id,
            HplcMethod = hplcMethodFp1,
            IsActive = true
        };
        var icpTest = new TestDefinition
        {
            Code = "ICP_AC",
            DisplayName = "ICP Heavy Metals Swab",
            WorkflowType = WorkflowType.IcpMethodAssay,
            SectionId = s.Section.Id,
            IcpMode = IcpTestMode.Assay,
            IsActive = true,
            IcpElements = new()
            {
                new() { Symbol = "Pb", TargetPpm = 10m, CleaningLimitPpm = 2m }
            }
        };
        var uvTest = new TestDefinition
        {
            Code = "UV_AC",
            DisplayName = "UV Absorbance Rinse",
            WorkflowType = WorkflowType.Measurement,
            SectionId = s.Section.Id,
            IsActive = true
        };
        db.TestDefinitions.AddRange(hplcTestFp1, icpTest, uvTest);

        // Machine Part Configurations
        var configHplcPart1 = new MachinePartConfiguration { MachinePartId = s.Part1.Id, TestType = "Swab", TestCode = "HPLC_AC", IsActive = true };
        var configHplcPart2 = new MachinePartConfiguration { MachinePartId = s.Part2.Id, TestType = "Swab", TestCode = "HPLC_AC", IsActive = true };
        var configIcpPart1 = new MachinePartConfiguration { MachinePartId = s.Part1.Id, TestType = "Swab", TestCode = "ICP_AC", IsActive = true };
        var configUvPart1 = new MachinePartConfiguration { MachinePartId = s.Part1.Id, TestType = "Rinse", TestCode = "UV_AC", IsActive = true };
        var configUvPart2 = new MachinePartConfiguration { MachinePartId = s.Part2.Id, TestType = "Rinse", TestCode = "UV_AC", IsActive = true };
        db.MachinePartConfigurations.AddRange(configHplcPart1, configHplcPart2, configIcpPart1, configUvPart1, configUvPart2);

        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        db.SaveChanges();

        // 1. Receive AC sample for FP2 (Ibuprofen), but HPLC_AC is configured with method for FP1 (Paracetamol)
        var receiveReqWrong = new AfterCleaningReceiveRequest(
            s.Machine.Id, cause.Id, "Sampler", "AC-WRONG", s.User.Id,
            PreviousProductName: s.FinishedProduct2.Name,
            PreviousProductBatchNumber: "B002",
            LabSectionId: s.Section.Id,
            PreviousProductItemId: s.FinishedProduct2.Id,
            Techniques: new() { "HPLC" });
        var wrongSample = await engine.ReceiveAsync(receiveReqWrong);

        var exRefusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.PrepareAsync(wrongSample.Id, new List<int> { configHplcPart1.Id }, s.User.Id));
        Assert.Equal($"No cleaning HPLC method for {s.FinishedProduct2.Name} on test HPLC_AC.", exRefusal.Message);

        // 2. Receive AC sample for FP1 (Paracetamol) - matches hplcMethodFp1
        var receiveReqOk = new AfterCleaningReceiveRequest(
            s.Machine.Id, cause.Id, "Sampler", "AC-OK", s.User.Id,
            PreviousProductName: s.FinishedProduct1.Name,
            PreviousProductBatchNumber: "B001",
            LabSectionId: s.Section.Id,
            PreviousProductItemId: s.FinishedProduct1.Id,
            Techniques: new() { "HPLC", "ICP", "UV" });
        var okSample = await engine.ReceiveAsync(receiveReqOk);

        var preparedSample = await engine.PrepareAsync(okSample.Id, new List<int>
        {
            configHplcPart1.Id, configHplcPart2.Id, // 2 HPLC configs -> 2 orders
            configIcpPart1.Id,                     // 1 ICP config -> 1 order
            configUvPart1.Id, configUvPart2.Id     // 2 UV configs -> 1 order with 2 locations
        }, s.User.Id);

        var orders = preparedSample.TestOrders.ToList();

        // Verify: 2 HPLC orders + 1 ICP order + 1 UV order = 4 orders
        Assert.Equal(4, orders.Count);
        var hplcOrders = orders.Where(o => o.TestCode == "HPLC_AC").ToList();
        var icpOrders = orders.Where(o => o.TestCode == "ICP_AC").ToList();
        var uvOrders = orders.Where(o => o.TestCode == "UV_AC").ToList();

        Assert.Equal(2, hplcOrders.Count);
        Assert.Single(icpOrders);
        Assert.Single(uvOrders);

        // Each HPLC & ICP order has exactly 1 SampleLocation
        var order1Locs = await db.SampleLocations.Where(l => l.TestOrderId == hplcOrders[0].Id).ToListAsync();
        Assert.Single(order1Locs);

        var order2Locs = await db.SampleLocations.Where(l => l.TestOrderId == hplcOrders[1].Id).ToListAsync();
        Assert.Single(order2Locs);

        var hplcConfigIds = new[] { order1Locs[0].MachinePartConfigurationId, order2Locs[0].MachinePartConfigurationId };
        Assert.Contains(configHplcPart1.Id, hplcConfigIds);
        Assert.Contains(configHplcPart2.Id, hplcConfigIds);

        var icpLocs = await db.SampleLocations.Where(l => l.TestOrderId == icpOrders[0].Id).ToListAsync();
        Assert.Single(icpLocs);
        Assert.Equal(configIcpPart1.Id, icpLocs[0].MachinePartConfigurationId);

        // UV order has 2 locations
        var uvLocs = await db.SampleLocations.Where(l => l.TestOrderId == uvOrders[0].Id).ToListAsync();
        Assert.Equal(2, uvLocs.Count);
    }

    [Fact]
    public async Task HplcSampleEntry_SyntheticSpecs_AndRecorder_NullSpecificationId()
    {
        await using var db = NewDb();
        var s = Seed(db);

        var hplcMethod = new HplcMethod
        {
            SectionId = s.Section.Id,
            Abbreviation = "M-HPLC-AC",
            Name = "Paracetamol Cleaning HPLC",
            Technique = HplcTechnique.Hplc,
            ResultMode = HplcResultMode.CleaningResidue,
            SampleSolutionVolumeMl = 10m,
            ProductItemId = s.FinishedProduct1.Id,
            Analytes = new()
            {
                new() { Name = "Paracetamol", DisplayOrder = 1, StandardConcentrationUgPerMl = 5m, MacoLimit = 50m, MacoUnit = MacoUnit.UgPerSwab }
            }
        };
        db.HplcMethods.Add(hplcMethod);

        var testDef = new TestDefinition
        {
            Code = "HPLC_AC_TEST",
            DisplayName = "HPLC Cleaning Swab",
            WorkflowType = WorkflowType.HplcMethodAssay,
            SectionId = s.Section.Id,
            HplcMethod = hplcMethod,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var sample = new Sample
        {
            Category = SampleCategory.AfterCleaning,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReferenceNumber = "AC-SMP-01",
            MachineId = s.Machine.Id,
            PreviousProductItemId = s.FinishedProduct1.Id,
            PreviousProductName = s.FinishedProduct1.Name,
            CleaningTechniques = "HPLC"
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            SectionId = s.Section.Id,
            TestCode = "HPLC_AC_TEST",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        var config = new MachinePartConfiguration { MachinePartId = s.Part1.Id, TestType = "Swab", TestCode = "HPLC_AC_TEST", IsActive = true };
        db.MachinePartConfigurations.Add(config);
        db.SaveChanges();

        var loc = new SampleLocation { SampleId = sample.Id, TestOrderId = order.Id, MachinePartConfigurationId = config.Id, LocationType = LocationType.MachinePart };
        db.SampleLocations.Add(loc);

        // Set up HPLC Run
        var run = new HplcRun
        {
            SectionId = s.Section.Id,
            Code = "RUN-01",
            EquipmentId = s.Equipment.Id,
            ChromatographyColumnId = s.Column.Id,
            HplcMethodId = hplcMethod.Id,
            AnalystUserId = s.User.Id,
            StartedAt = DateTime.UtcNow,
            Status = HplcRunStatus.Open,
            MethodSnapshotJson = JsonSerializer.Serialize(HplcMethodResponse.From(hplcMethod), SnapshotJson.Options),
            Sst = new HplcSstRecord
            {
                Code = "SST-01",
                Status = HplcSstStatus.Passed,
                Analytes = new()
                {
                    new()
                    {
                        HplcMethodAnalyteId = hplcMethod.Analytes[0].Id,
                        MeanResponse = 2000m,
                        Passed = true
                    }
                }
            }
        };
        db.HplcRuns.Add(run);
        db.SaveChanges();

        var runSample = new HplcRunSample
        {
            HplcRunId = run.Id,
            TestOrderId = order.Id,
            Status = HplcRunSampleStatus.Assigned,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = s.User.Id
        };
        db.HplcRunSamples.Add(runSample);
        db.SaveChanges();

        // 1. Load context
        var ctx = await HplcSampleEntryContext.LoadAsync(db, runSample.Id);
        Assert.True(ctx.IsCleaningResidue);
        Assert.Single(ctx.Specs);
        var spec = ctx.Specs[0];
        Assert.Equal(0, spec.Id);
        Assert.Equal(50m, spec.UpperLimit);
        Assert.Equal("µg/swab", spec.Unit);
        Assert.Equal("Paracetamol", spec.ParameterName);

        // Add replicate response: 1000 => (1000 / 2000) * 5 * 10 = 25 µg/swab <= 50 (WithinLimits)
        runSample.Replicates.Add(new HplcSampleReplicate
        {
            ReplicateNo = 1,
            ActualWeightMg = 0m,
            Responses = new() { new() { HplcMethodAnalyteId = hplcMethod.Analytes[0].Id, Response = 1000m } }
        });
        db.SaveChanges();

        ctx = await HplcSampleEntryContext.LoadAsync(db, runSample.Id);
        Assert.Null(ctx.EntryProblem());
        var rows = ctx.EvaluateRows();
        Assert.Single(rows);
        Assert.Equal(25m, rows[0].Value);
        Assert.Equal(ResultStatus.WithinLimits, rows[0].Status);
        Assert.Equal("25.00 µg/swab", rows[0].Display);

        // Upload sample report evidence so submit can proceed
        db.HplcEvidences.Add(new HplcEvidence
        {
            HplcRunId = run.Id,
            HplcRunSampleId = runSample.Id,
            Context = HplcEvidenceContext.Sample,
            Kind = HplcEvidenceKind.SampleReport,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            FilePath = "evidences/report.pdf",
            UploadedByUserId = s.User.Id,
            UploadedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        // 2. Submit via TestWorkflowEngine (HplcMethodAssayRecorder)
        var workflow = TestServiceFactory.TestWorkflow(db);
        var result = await workflow.SubmitHplcMethodAssayAsync(runSample.Id, Password, "AC test complete", s.User.Id, "127.0.0.1");
        Assert.True(result.AllStepsComplete);

        var paramResults = await db.ParameterResults.Where(p => p.TestOrderId == order.Id).ToListAsync();
        Assert.Single(paramResults);
        var pr = paramResults[0];
        Assert.Null(pr.SpecificationId); // SpecificationId must be null for synthetic spec!
        Assert.Equal("Paracetamol", pr.ParameterName);
        Assert.Equal("25.00 µg/swab", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);

        var updatedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Ready, updatedOrder!.CurrentStep);
    }

    [Fact]
    public async Task IcpSampleEntry_SyntheticSpecs_AndRecorder_NullSpecificationId()
    {
        await using var db = NewDb();
        var s = Seed(db);

        var testDef = new TestDefinition
        {
            Code = "ICP_AC_TEST",
            DisplayName = "ICP Cleaning Swab",
            WorkflowType = WorkflowType.IcpMethodAssay,
            SectionId = s.Section.Id,
            IcpMode = IcpTestMode.Assay,
            IsActive = true,
            IcpElements = new()
            {
                new() { Symbol = "Pb", TargetPpm = 10m, CleaningLimitPpm = 2m, DisplayOrder = 1 },
                new() { Symbol = "Cd", TargetPpm = 5m, CleaningLimitPpm = null, DisplayOrder = 2 } // no cleaning limit
            }
        };
        db.TestDefinitions.Add(testDef);

        var sample = new Sample
        {
            Category = SampleCategory.AfterCleaning,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReferenceNumber = "AC-SMP-02",
            MachineId = s.Machine.Id,
            PreviousProductItemId = s.FinishedProduct1.Id,
            PreviousProductName = s.FinishedProduct1.Name,
            CleaningTechniques = "ICP"
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            SectionId = s.Section.Id,
            TestCode = "ICP_AC_TEST",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        var config = new MachinePartConfiguration { MachinePartId = s.Part1.Id, TestType = "Swab", TestCode = "ICP_AC_TEST", IsActive = true };
        db.MachinePartConfigurations.Add(config);
        db.SaveChanges();

        var loc = new SampleLocation { SampleId = sample.Id, TestOrderId = order.Id, MachinePartConfigurationId = config.Id, LocationType = LocationType.MachinePart };
        db.SampleLocations.Add(loc);

        // Set up ICP Run
        var cal = new IcpCalibration
        {
            Code = "CAL-01",
            Status = IcpCalibrationStatus.Confirmed,
            ConfirmedAt = DateTime.UtcNow,
            ConfirmedByUserId = s.User.Id,
            Elements = new()
            {
                new() { Symbol = "Pb", CorrelationR = 0.9999m, Passed = true },
                new() { Symbol = "Cd", CorrelationR = 0.9999m, Passed = true }
            }
        };
        var run = new IcpRun
        {
            SectionId = s.Section.Id,
            Code = "RUN-ICP-01",
            EquipmentId = s.Equipment.Id,
            AnalystUserId = s.User.Id,
            StartedAt = DateTime.UtcNow,
            Status = IcpRunStatus.Open,
            Calibration = cal
        };
        db.IcpRuns.Add(run);
        db.SaveChanges();

        var runSample = new IcpRunSample
        {
            IcpRunId = run.Id,
            TestOrderId = order.Id,
            Status = IcpRunSampleStatus.Assigned,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = s.User.Id
        };
        db.IcpRunSamples.Add(runSample);
        db.SaveChanges();

        // 1. Load context
        var ctx = await IcpSampleEntryContext.LoadAsync(db, runSample.Id);
        // Only elements with CleaningLimitPpm get synthetic specs: only Pb
        Assert.Single(ctx.Specs);
        var spec = ctx.Specs[0];
        Assert.Equal(0, spec.Id);
        Assert.Equal(2m, spec.UpperLimit);
        Assert.Equal("ppm", spec.Unit);
        Assert.Equal("Pb", spec.ParameterName);
        Assert.False(ctx.NeedsWeight); // AfterCleaning ICP does not need sample weight

        // Enter result: 1.5 ppm <= 2.0 ppm
        runSample.Concentrations.Add(new IcpSampleConcentration
        {
            IcpTestElementId = testDef.IcpElements[0].Id,
            ResultPpm = 1.5m
        });
        db.SaveChanges();

        ctx = await IcpSampleEntryContext.LoadAsync(db, runSample.Id);
        Assert.Null(ctx.EntryProblem());
        var rows = ctx.EvaluateRows();
        Assert.Single(rows);
        Assert.Equal(1.5m, rows[0].Value);
        Assert.Equal(ResultStatus.WithinLimits, rows[0].Status);

        // Upload sample report evidence so submit can proceed
        db.IcpEvidences.Add(new IcpEvidence
        {
            IcpRunId = run.Id,
            IcpRunSampleId = runSample.Id,
            Context = IcpEvidenceContext.Sample,
            Kind = IcpEvidenceKind.SampleReport,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            FilePath = "evidences/report.pdf",
            UploadedByUserId = s.User.Id,
            UploadedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        // 2. Submit via TestWorkflowEngine (IcpMethodAssayRecorder)
        var workflow = TestServiceFactory.TestWorkflow(db);
        var result = await workflow.SubmitIcpMethodAssayAsync(runSample.Id, Password, "ICP AC test complete", s.User.Id, "127.0.0.1");
        Assert.True(result.AllStepsComplete);

        var paramResults = await db.ParameterResults.Where(p => p.TestOrderId == order.Id).ToListAsync();
        Assert.Single(paramResults);
        var pr = paramResults[0];
        Assert.Null(pr.SpecificationId); // SpecificationId must be null for synthetic spec!
        Assert.Equal("Pb", pr.ParameterName);
        Assert.Equal("1.5 ppm", pr.ReportedDisplay);
        Assert.Equal(ResultStatus.WithinLimits, pr.ComparisonStatus);

        var updatedOrder = await db.TestOrders.FindAsync(order.Id);
        Assert.Equal(WorkflowStep.Ready, updatedOrder!.CurrentStep);
    }

    [Fact]
    public async Task SubjectName_OnRunDtos_AndSampleSummaryBuilder()
    {
        await using var db = NewDb();
        var s = Seed(db);

        var hplcMethod = new HplcMethod
        {
            SectionId = s.Section.Id,
            Abbreviation = "M-HPLC-B5",
            Name = "Paracetamol Cleaning HPLC",
            Technique = HplcTechnique.Hplc,
            ResultMode = HplcResultMode.CleaningResidue,
            SampleSolutionVolumeMl = 10m,
            ProductItemId = s.FinishedProduct1.Id,
            Analytes = new()
            {
                new() { Name = "Paracetamol", DisplayOrder = 1, StandardConcentrationUgPerMl = 5m, MacoLimit = 50m, MacoUnit = MacoUnit.UgPerSwab }
            }
        };
        db.HplcMethods.Add(hplcMethod);

        var testDef = new TestDefinition
        {
            Code = "HPLC_B5",
            DisplayName = "HPLC Cleaning Swab",
            WorkflowType = WorkflowType.HplcMethodAssay,
            SectionId = s.Section.Id,
            HplcMethod = hplcMethod,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        var sample = new Sample
        {
            Category = SampleCategory.AfterCleaning,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReferenceNumber = "AC-B5-01",
            MachineId = s.Machine.Id,
            Machine = s.Machine,
            PreviousProductItemId = s.FinishedProduct1.Id,
            PreviousProductName = s.FinishedProduct1.Name,
            CleaningTechniques = "HPLC"
        };
        db.Samples.Add(sample);
        db.SaveChanges();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            SectionId = s.Section.Id,
            TestCode = "HPLC_B5",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting
        };
        db.TestOrders.Add(order);
        db.SaveChanges();

        var config = new MachinePartConfiguration { MachinePartId = s.Part1.Id, MachinePart = s.Part1, TestType = "Swab", TestCode = "HPLC_B5", IsActive = true };
        db.MachinePartConfigurations.Add(config);
        db.SaveChanges();

        var loc = new SampleLocation { SampleId = sample.Id, TestOrderId = order.Id, MachinePartConfigurationId = config.Id, MachinePartConfiguration = config, LocationType = LocationType.MachinePart };
        db.SampleLocations.Add(loc);

        var run = new HplcRun
        {
            SectionId = s.Section.Id,
            Code = "RUN-B5",
            EquipmentId = s.Equipment.Id,
            ChromatographyColumnId = s.Column.Id,
            HplcMethodId = hplcMethod.Id,
            AnalystUserId = s.User.Id,
            StartedAt = DateTime.UtcNow,
            Status = HplcRunStatus.Open,
            MethodSnapshotJson = JsonSerializer.Serialize(HplcMethodResponse.From(hplcMethod), SnapshotJson.Options),
            Sst = new HplcSstRecord { Code = "SST-B5", Status = HplcSstStatus.Passed }
        };
        db.HplcRuns.Add(run);
        db.SaveChanges();

        var hplcService = TestServiceFactory.HplcRun(db);

        // 1. Check EligibleTestDto has SubjectName: "Granulator 01 – Impeller"
        var eligible = await hplcService.GetEligibleTestsAsync(run.Id, null, s.User.Id);
        Assert.Single(eligible);
        Assert.Equal("Granulator 01 \u2013 Impeller", eligible[0].SubjectName);

        // Assign sample
        await hplcService.AssignSamplesAsync(run.Id, new() { order.Id }, s.User.Id);

        // 2. Check HplcRunSampleSummaryDto has SubjectName: "Granulator 01 – Impeller"
        var runDto = await hplcService.GetRunAsync(run.Id, s.User.Id);
        Assert.Single(runDto.Samples);
        Assert.Equal("Granulator 01 \u2013 Impeller", runDto.Samples[0].SubjectName);

        // 3. Check HplcSampleEntryDto has SubjectName
        var entryDto = await hplcService.GetSampleEntryAsync(runDto.Samples[0].Id, s.User.Id);
        Assert.Equal("Granulator 01 \u2013 Impeller", entryDto.SubjectName);

        // 4. Test SampleSummaryTestOrderBuilder
        var activeAnalysisRow = new ActiveAnalysisRow
        {
            Id = 1,
            TestOrderId = order.Id,
            AnalysisType = WorkflowType.HplcMethodAssay,
            EnteredByUserId = s.User.Id,
            EnteredAt = DateTime.UtcNow,
            Results = new()
            {
                new ActiveParameterResultRow
                {
                    Id = 1,
                    SpecificationId = null,
                    ParameterName = "Paracetamol",
                    ReportedDisplay = "25.00 µg/swab",
                    ReportedValue = 25m,
                    ComparisonStatus = ResultStatus.WithinLimits
                }
            }
        };

        // Load summary
        var summaryData = new SampleSummaryData
        {
            Sample = sample,
            EffectiveTestOrders = new() { order },
            TestDefinitions = new() { { "HPLC_B5", testDef } },
            Incubations = new(),
            MediaLookup = new(),
            LocationPathogenObservations = new(),
            SampleLocations = new() { loc },
            SpecificationsByTestCode = new(),
            SectionRows = new() { { s.Section.Id, new SampleSectionRow(s.Section.Code, s.Section.Name) } },
            SourceRefByOrderId = new(),
            Results = new(),
            CountTestReadings = new(),
            ActiveAnalyses = new() { activeAnalysisRow },
            PathogenObservations = new(),
            BiochemicalResults = new(),
            WorkflowHistory = new(),
            Now = DateTime.UtcNow
        };

        var orderSummary = SampleSummaryTestOrderBuilder.Build(order, summaryData, _ => "Chemist");
        // TestDisplayName must include part name: "HPLC Cleaning Swab – Impeller"
        Assert.Equal("HPLC Cleaning Swab \u2013 Impeller", orderSummary.TestDisplayName);
        // Locations list must be empty (no empty location row)
        Assert.Empty(orderSummary.Locations);
    }
}
