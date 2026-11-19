using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

internal static class QaScopeTestSeed
{
    public const string Password = "QaScope-Test-9!";

    public sealed record World(
        string Suffix,
        int DepartmentId,
        int SectionAId,
        int SectionBId,
        int QaUserId,
        int QaRoleId,
        int AnalystAId,
        int AnalystA2Id,
        int AnalystBId,
        int SampleAId,
        int SampleBId,
        int ReviewSampleAId,
        int ApprovalSampleAId,
        int RetestSampleAId,
        int RetestSampleBId,
        string OosGroupA,
        string OosGroupB,
        int OrderAId,
        int OrderBId,
        int UvOrderAId,
        int IncubationAId,
        int IncubationBId,
        int MaterialAId,
        int MaterialBId,
        int DehydratedMaterialAId,
        int RefMaterialAId,
        int MediaAId,
        int MediaBId,
        int MediaEvaluationAId,
        int MediaEvaluationBId,
        int MediaChallengeAId,
        int MediaChallengeBId,
        int CryovialAId,
        int CryovialBId,
        int EquipmentAId,
        int EquipmentBId,
        int AutoclaveEquipmentAId,
        int UvEquipmentAId,
        int EquipmentInventoryAId,
        int EquipmentInventoryBId,
        int ColumnAId,
        int ColumnBId,
        int HplcMethodAId,
        int HplcRunAId,
        int SolutionMasterAId,
        int SolutionPrepAId,
        int WorkingStandardQualificationAId,
        int UvSpecAId);

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public static async Task<World> SeedWorldAsync(PostgresTestFixture fixture, CancellationToken ct = default)
    {
        var uid = UniqueSuffix();
        await using var db = fixture.CreateDbContext();

        var micro = TestServiceFactory.EnsureMicroSection(db);
        var deptId = micro.DepartmentId;

        var secA = new DocumentSection
        {
            Name = $"QA Scope Section A {uid}",
            Code = $"QSA-{uid}",
            DepartmentId = deptId,
            IsActive = true
        };
        var secB = new DocumentSection
        {
            Name = $"QA Scope Section B {uid}",
            Code = $"QSB-{uid}",
            DepartmentId = deptId,
            IsActive = true
        };
        db.DocumentSections.AddRange(secA, secB);
        await db.SaveChangesAsync(ct);

        var qaRole = new Role
        {
            Type = RoleType.Qa,
            Name = $"QA-{uid}",
            IsSystemRole = false,
            IsActive = true
        };
        db.Roles.Add(qaRole);
        await db.SaveChangesAsync(ct);

        var qaPermissions = await db.Permissions
            .Where(p => PermissionConstants.QaGrants.Contains(p.Code))
            .ToListAsync(ct);
        Assert.Equal(PermissionConstants.QaGrants.Count, qaPermissions.Count);
        foreach (var p in qaPermissions)
            db.RolePermissions.Add(new RolePermission { RoleId = qaRole.Id, PermissionId = p.Id });
        await db.SaveChangesAsync(ct);

        var analystRole = await db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Analyst, ct);
        if (analystRole is null)
        {
            analystRole = new Role { Type = RoleType.Analyst, Name = $"Analyst-{uid}", IsActive = true };
            db.Roles.Add(analystRole);
            await db.SaveChangesAsync(ct);
        }

        var pwHash = TestPasswords.Hash(Password);
        var qaUser = new User
        {
            FullName = $"QA Reader {uid}",
            Username = $"qa_{uid}".ToLowerInvariant(),
            Email = $"qa_{uid.ToLowerInvariant()}@test.local",
            RoleId = qaRole.Id,
            PasswordHash = pwHash,
            IsActive = true,
            // The fixture database is shared. An active, unlocked holder of Access.Review.Sign
            // switches access review signing over to QA for every later test, so this reader is
            // kept locked: section scope does not look at the lock, the signer rule does.
            LockedUntil = new DateTime(2999, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var analystA = new User
        {
            FullName = $"Analyst A {uid}",
            Username = $"ana_{uid}".ToLowerInvariant(),
            Email = $"ana_{uid.ToLowerInvariant()}@test.local",
            RoleId = analystRole.Id,
            PasswordHash = pwHash,
            IsActive = true
        };
        var analystA2 = new User
        {
            FullName = $"Analyst A2 {uid}",
            Username = $"ana2_{uid}".ToLowerInvariant(),
            Email = $"ana2_{uid.ToLowerInvariant()}@test.local",
            RoleId = analystRole.Id,
            PasswordHash = pwHash,
            IsActive = true
        };
        var analystB = new User
        {
            FullName = $"Analyst B {uid}",
            Username = $"anb_{uid}".ToLowerInvariant(),
            Email = $"anb_{uid.ToLowerInvariant()}@test.local",
            RoleId = analystRole.Id,
            PasswordHash = pwHash,
            IsActive = true
        };
        db.Users.AddRange(qaUser, analystA, analystA2, analystB);
        await db.SaveChangesAsync(ct);

        // QA deliberately receives NO UserOrgMembership; Analyst A/A2 -> Section A only; Analyst B -> Section B only.
        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = analystA.Id, DepartmentId = deptId, SectionId = secA.Id },
            new UserOrgMembership { UserId = analystA2.Id, DepartmentId = deptId, SectionId = secA.Id },
            new UserOrgMembership { UserId = analystB.Id, DepartmentId = deptId, SectionId = secB.Id });
        await db.SaveChangesAsync(ct);

        // Equipment per section (including autoclave, HPLC, and UV equipment in Section A)
        var eqA = new Equipment
        {
            Name = $"Incubator A {uid}",
            Code = $"EQA-{uid}",
            Type = EquipmentType.Incubator,
            SectionId = secA.Id,
            SetPointTemperature = 32.5m
        };
        var eqB = new Equipment
        {
            Name = $"Incubator B {uid}",
            Code = $"EQB-{uid}",
            Type = EquipmentType.Incubator,
            SectionId = secB.Id,
            SetPointTemperature = 32.5m
        };
        var autoclaveEqA = new Equipment
        {
            Name = $"Autoclave A {uid}",
            Code = $"AUTA-{uid}",
            Type = EquipmentType.Autoclave,
            SectionId = secA.Id
        };
        var hplcEqA = new Equipment
        {
            Name = $"HPLC A {uid}",
            Code = $"HPLCA-{uid}",
            Type = EquipmentType.Hplc,
            SectionId = secA.Id
        };
        var uvEqA = new Equipment
        {
            Name = $"UV Spec A {uid}",
            Code = $"UVA-{uid}",
            Type = EquipmentType.Other,
            SectionId = secA.Id
        };
        db.Equipment.AddRange(eqA, eqB, autoclaveEqA, hplcEqA, uvEqA);

        var invA = new EquipmentInventory
        {
            InstrumentType = "Incubator",
            ManufacturerName = "TestCo",
            Code = $"EIA-{uid}",
            Location = "Lab A",
            SectionId = secA.Id,
            CreatedByUserId = analystA.Id
        };
        var invB = new EquipmentInventory
        {
            InstrumentType = "Incubator",
            ManufacturerName = "TestCo",
            Code = $"EIB-{uid}",
            Location = "Lab B",
            SectionId = secB.Id,
            CreatedByUserId = analystB.Id
        };
        db.EquipmentInventories.AddRange(invA, invB);

        var colA = new ChromatographyColumn
        {
            Code = $"COLA-{uid}",
            Name = $"Column A {uid}",
            SectionId = secA.Id,
            CreatedByUserId = analystA.Id,
            LastModifiedByUserId = analystA.Id
        };
        var colB = new ChromatographyColumn
        {
            Code = $"COLB-{uid}",
            Name = $"Column B {uid}",
            SectionId = secB.Id,
            CreatedByUserId = analystB.Id,
            LastModifiedByUserId = analystB.Id
        };
        db.ChromatographyColumns.AddRange(colA, colB);

        var strain = new Organism { ScientificName = $"Test Strain {uid}" };
        db.Organisms.Add(strain);

        var mediaProdA = new MediaProduct
        {
            Name = $"Test Broth A {uid}",
            Code = $"M{uid}"
        };
        db.MediaProducts.Add(mediaProdA);

        var matMasterA = new MaterialMasterEntry
        {
            SectionId = secA.Id,
            Code = $"MME-A-{uid}",
            Name = $"Ref Standard Master {uid}",
            Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Gram,
            CreatedByUserId = analystA.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.Add(matMasterA);
        await db.SaveChangesAsync(ct);

        // Materials per section
        var lyoMatA = new Material
        {
            SectionId = secA.Id,
            MaterialType = MaterialType.LyophilizedMicroorganism,
            MaterialName = $"Strain Stock A {uid}",
            ManufacturerName = "BioTest",
            BatchNumber = $"B-LYO-A-{uid}",
            StockId = $"STK-A-{uid}",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            OrganismId = strain.Id,
            QuantityReceived = 10,
            QuantityRemaining = 10,
            Unit = MaterialUnit.Vial,
            CreatedByUserId = analystA.Id
        };
        var lyoMatB = new Material
        {
            SectionId = secB.Id,
            MaterialType = MaterialType.LyophilizedMicroorganism,
            MaterialName = $"Strain Stock B {uid}",
            ManufacturerName = "BioTest",
            BatchNumber = $"B-LYO-B-{uid}",
            StockId = $"STK-B-{uid}",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            OrganismId = strain.Id,
            QuantityReceived = 10,
            QuantityRemaining = 10,
            Unit = MaterialUnit.Vial,
            CreatedByUserId = analystB.Id
        };
        var dehydMatA = new Material
        {
            SectionId = secA.Id,
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = mediaProdA.Name,
            MediaProductId = mediaProdA.Id,
            Code = mediaProdA.Code,
            ManufacturerName = "MediaCo",
            BatchNumber = $"B-MED-A-{uid}",
            StockId = $"STK-MA-{uid}",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = analystA.Id
        };
        var refMatA = new Material
        {
            SectionId = secA.Id,
            MaterialType = MaterialType.ReferenceStandard,
            MaterialMasterEntryId = matMasterA.Id,
            MaterialName = $"Ref Std A {uid}",
            ManufacturerName = "StdCo",
            BatchNumber = $"B-STD-A-{uid}",
            StockId = $"STK-RA-{uid}",
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 5,
            QuantityRemaining = 5,
            Unit = MaterialUnit.Gram,
            Purity = 99.5m,
            ApprovalStatus = ApprovalGateStatus.PendingReview,
            CreatedByUserId = analystA.Id
        };
        db.Materials.AddRange(lyoMatA, lyoMatB, dehydMatA, refMatA);
        await db.SaveChangesAsync(ct);

        var mediaA = new Media
        {
            MaterialId = lyoMatA.Id,
            LotNumber = $"LOT-A-{uid}",
            IsReleasedForUse = true,
            Status = MediaStatus.Active,
            ApprovalStatus = ApprovalGateStatus.Approved,
            PreparedByUserId = analystA.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        var mediaB = new Media
        {
            MaterialId = lyoMatB.Id,
            LotNumber = $"LOT-B-{uid}",
            IsReleasedForUse = true,
            Status = MediaStatus.Active,
            ApprovalStatus = ApprovalGateStatus.Approved,
            PreparedByUserId = analystB.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        db.Media.AddRange(mediaA, mediaB);
        await db.SaveChangesAsync(ct);

        var evalA = new MediaEvaluation { MediaId = mediaA.Id, EvaluationType = EvaluationType.GrowthPromotion };
        var chalA = new MediaEvaluationChallenge { OrganismId = strain.Id };
        evalA.Challenges.Add(chalA);
        var evalB = new MediaEvaluation { MediaId = mediaB.Id, EvaluationType = EvaluationType.GrowthPromotion };
        var chalB = new MediaEvaluationChallenge { OrganismId = strain.Id };
        evalB.Challenges.Add(chalB);
        db.MediaEvaluations.AddRange(evalA, evalB);

        var cryoA = new Cryovial
        {
            MaterialId = lyoMatA.Id,
            Code = $"CRYO-A-{uid}",
            OrganismId = strain.Id,
            OrganismNameSnapshot = strain.ScientificName,
            ManufacturerName = "BioTest",
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            NumberOfVialsPrepared = 5,
            VialsRemaining = 5,
            PreparedByUserId = analystA.Id,
            ApprovalStatus = ApprovalGateStatus.Approved
        };
        var cryoB = new Cryovial
        {
            MaterialId = lyoMatB.Id,
            Code = $"CRYO-B-{uid}",
            OrganismId = strain.Id,
            OrganismNameSnapshot = strain.ScientificName,
            ManufacturerName = "BioTest",
            ExpiryDate = DateTime.UtcNow.AddDays(30),
            NumberOfVialsPrepared = 5,
            VialsRemaining = 5,
            PreparedByUserId = analystB.Id,
            ApprovalStatus = ApprovalGateStatus.Approved
        };
        db.Cryovials.AddRange(cryoA, cryoB);
        await db.SaveChangesAsync(ct);

        // TestDefinitions (Observation for EnsurePathogenSampleAccessAsync, UV for TestWorkflow.Execute)
        var tdObsA = new TestDefinition
        {
            Code = $"TDOA-{uid}",
            DisplayName = $"Obs Test A {uid}",
            SectionId = secA.Id,
            WorkflowType = WorkflowType.Observation,
            IsActive = true
        };
        var tdObsB = new TestDefinition
        {
            Code = $"TDOB-{uid}",
            DisplayName = $"Obs Test B {uid}",
            SectionId = secB.Id,
            WorkflowType = WorkflowType.Observation,
            IsActive = true
        };
        var tdUvA = new TestDefinition
        {
            Code = $"TDUV-{uid}",
            DisplayName = $"UV Test A {uid}",
            SectionId = secA.Id,
            WorkflowType = WorkflowType.UvSpectrophotometry,
            EquationType = EquationType.UvSpectrophotometry,
            UvCalculationMode = UvCalculationMode.AbsorbanceLimit,
            ReplicateCount = 1,
            IsActive = true
        };
        db.TestDefinitions.AddRange(tdObsA, tdObsB, tdUvA);

        var itemA = new Item
        {
            Code = $"ITMA-{uid}",
            Name = $"Item A {uid}",
            Category = SampleCategory.RawMaterial,
            IsActive = true
        };
        db.Items.Add(itemA);
        var cause = await db.CausesOfTesting.FirstAsync(ct);
        await db.SaveChangesAsync(ct);

        var uvSpecA = new Specification
        {
            ItemId = itemA.Id,
            TestCode = tdUvA.Code,
            ParameterName = "Absorbance",
            LimitType = LimitType.NotMoreThan,
            UpperLimit = 0.5m
        };
        db.Specifications.Add(uvSpecA);

        // Samples + TestOrders across Section A and Section B
        var sampleA = new Sample
        {
            ReferenceNumber = $"SM-A-{uid}",
            Category = SampleCategory.RawMaterial,
            ItemId = itemA.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler A",
            ControlNumber = $"CTRL-A-{uid}",
            Status = SampleStatus.InTesting,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReceivedByUserId = analystA.Id,
            ReceivedAt = DateTime.UtcNow.AddDays(-1)
        };
        var sampleB = new Sample
        {
            ReferenceNumber = $"SM-B-{uid}",
            Category = SampleCategory.RawMaterial,
            ItemId = itemA.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler B",
            ControlNumber = $"CTRL-B-{uid}",
            Status = SampleStatus.InTesting,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReceivedByUserId = analystB.Id,
            ReceivedAt = DateTime.UtcNow.AddDays(-1)
        };
        var reviewSampleA = new Sample
        {
            ReferenceNumber = $"RV-A-{uid}",
            Category = SampleCategory.RawMaterial,
            ItemId = itemA.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler A",
            ControlNumber = $"CTRL-RV-{uid}",
            Status = SampleStatus.UnderReview,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReceivedByUserId = analystA.Id,
            ReceivedAt = DateTime.UtcNow.AddDays(-2)
        };
        var approvalSampleA = new Sample
        {
            ReferenceNumber = $"AP-A-{uid}",
            Category = SampleCategory.RawMaterial,
            ItemId = itemA.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler A",
            ControlNumber = $"CTRL-AP-{uid}",
            Status = SampleStatus.UnderApproval,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReviewedByUserId = analystA.Id,
            ReviewedAt = DateTime.UtcNow.AddHours(-2),
            ReceivedByUserId = analystA.Id,
            ReceivedAt = DateTime.UtcNow.AddDays(-3)
        };
        db.Samples.AddRange(sampleA, sampleB, reviewSampleA, approvalSampleA);
        await db.SaveChangesAsync(ct);

        var oosA = $"OOS-A-{uid}";
        var oosB = $"OOS-B-{uid}";
        var retestA = new Sample
        {
            ReferenceNumber = $"RT-A-{uid}",
            Category = SampleCategory.RawMaterial,
            ItemId = itemA.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler A",
            ControlNumber = $"CTRL-RTA-{uid}",
            Status = SampleStatus.InTesting,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReceivedByUserId = analystA.Id,
            OriginSampleId = sampleA.Id,
            OosGroupCode = oosA
        };
        var retestB = new Sample
        {
            ReferenceNumber = $"RT-B-{uid}",
            Category = SampleCategory.RawMaterial,
            ItemId = itemA.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler B",
            ControlNumber = $"CTRL-RTB-{uid}",
            Status = SampleStatus.InTesting,
            PreparationStatus = SamplePreparationStatus.Ready,
            ReceivedByUserId = analystB.Id,
            OriginSampleId = sampleB.Id,
            OosGroupCode = oosB
        };
        db.Samples.AddRange(retestA, retestB);
        await db.SaveChangesAsync(ct);

        var orderA = new TestOrder
        {
            SampleId = sampleA.Id,
            TestCode = tdObsA.Code,
            SectionId = secA.Id,
            CurrentStep = WorkflowStep.Waiting,
            Status = ApprovalStatus.Pending
        };
        var orderB = new TestOrder
        {
            SampleId = sampleB.Id,
            TestCode = tdObsB.Code,
            SectionId = secB.Id,
            CurrentStep = WorkflowStep.Waiting,
            Status = ApprovalStatus.Pending
        };
        var uvOrderA = new TestOrder
        {
            SampleId = sampleA.Id,
            TestCode = tdUvA.Code,
            SectionId = secA.Id,
            CurrentStep = WorkflowStep.Waiting,
            Status = ApprovalStatus.Pending
        };
        var reviewOrderA = new TestOrder
        {
            SampleId = reviewSampleA.Id,
            TestCode = tdObsA.Code,
            SectionId = secA.Id,
            CurrentStep = WorkflowStep.Ready,
            Status = ApprovalStatus.Pending
        };
        var approvalOrderA = new TestOrder
        {
            SampleId = approvalSampleA.Id,
            TestCode = tdObsA.Code,
            SectionId = secA.Id,
            CurrentStep = WorkflowStep.Reviewed,
            Status = ApprovalStatus.Reviewed
        };
        var retestOrderA = new TestOrder
        {
            SampleId = retestA.Id,
            TestCode = tdObsA.Code,
            SectionId = secA.Id,
            CurrentStep = WorkflowStep.Waiting,
            Status = ApprovalStatus.Pending
        };
        var retestOrderB = new TestOrder
        {
            SampleId = retestB.Id,
            TestCode = tdObsB.Code,
            SectionId = secB.Id,
            CurrentStep = WorkflowStep.Waiting,
            Status = ApprovalStatus.Pending
        };
        db.TestOrders.AddRange(orderA, orderB, uvOrderA, reviewOrderA, approvalOrderA, retestOrderA, retestOrderB);
        await db.SaveChangesAsync(ct);

        var incA = new Incubation
        {
            TestOrderId = orderA.Id,
            StepNumber = 1,
            StepName = "Broth",
            MediaId = mediaA.Id,
            IncubatorEquipmentId = eqA.Id,
            StartedByUserId = analystA.Id,
            IncubationStartUtc = DateTime.UtcNow.AddHours(-2),
            IncubationEndUtc = DateTime.UtcNow.AddHours(22)
        };
        var incB = new Incubation
        {
            TestOrderId = orderB.Id,
            StepNumber = 1,
            StepName = "Broth",
            MediaId = mediaB.Id,
            IncubatorEquipmentId = eqB.Id,
            StartedByUserId = analystB.Id,
            IncubationStartUtc = DateTime.UtcNow.AddHours(-2),
            IncubationEndUtc = DateTime.UtcNow.AddHours(22)
        };
        db.Incubations.AddRange(incA, incB);

        // SolutionMaster before HplcMethod (HplcMethod.DiluentSolutionId is a required FK to SolutionMasters).
        var solMasterA = new SolutionMaster
        {
            Name = $"Sol Master A {uid}",
            SectionId = secA.Id,
            Type = SolutionType.MobilePhase,
            ShelfLifeValue = 7,
            ShelfLifeUnit = ShelfLifeUnit.Days,
            FinalVolumeMl = 1000m,
            IsActive = true,
            CreatedByUserId = analystA.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = analystA.Id,
            LastModifiedAt = DateTime.UtcNow
        };
        db.SolutionMasters.Add(solMasterA);
        await db.SaveChangesAsync(ct);

        var hplcMethodA = new HplcMethod
        {
            Name = $"HPLC Method A {uid}",
            Abbreviation = $"HM{uid[..4]}",
            SectionId = secA.Id,
            DiluentSolutionId = solMasterA.Id,
            EffectiveDate = DateTime.UtcNow,
            ColumnDesignation = "L1",
            ColumnLengthMm = 250m,
            ColumnInternalDiameterMm = 4.6m,
            FlowRateMlPerMin = 1.0m,
            InjectionVolumeUl = 10m,
            IsActive = true,
            CreatedByUserId = analystA.Id,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = analystA.Id,
            LastModifiedAt = DateTime.UtcNow
        };
        hplcMethodA.MobilePhases.Add(new HplcMethodMobilePhase { Channel = "A", SolutionMasterId = solMasterA.Id });
        db.HplcMethods.Add(hplcMethodA);
        await db.SaveChangesAsync(ct);

        var hplcRunA = new HplcRun
        {
            Code = $"HRUN-A-{uid}",
            SectionId = secA.Id,
            EquipmentId = hplcEqA.Id,
            ChromatographyColumnId = colA.Id,
            HplcMethodId = hplcMethodA.Id,
            Status = HplcRunStatus.Open,
            AnalystUserId = analystA.Id,
            StartedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.HplcRuns.Add(hplcRunA);

        var solPrepA = new SolutionPreparation
        {
            SectionId = secA.Id,
            SolutionMasterId = solMasterA.Id,
            Type = SolutionType.MobilePhase,
            Code = $"SP-A-{uid}",
            Status = SolutionPreparationStatus.Prepared,
            ApprovalStatus = ApprovalGateStatus.PendingReview,
            StartedByUserId = analystA.Id,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            PreparedByUserId = analystA.Id,
            PreparedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(10)
        };
        db.SolutionPreparations.Add(solPrepA);

        var wsQualA = new WorkingStandardQualification
        {
            SectionId = secA.Id,
            Code = $"WSQ-A-{uid}",
            Kind = WorkingStandardQualificationKind.Initial,
            Status = WorkingStandardQualificationStatus.Draft,
            MaterialMasterEntryId = matMasterA.Id,
            SourceMaterialName = $"Ref Std A {uid}",
            SourceBatchNumber = $"B-STD-A-{uid}",
            CreatedByUserId = analystA.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.WorkingStandardQualifications.Add(wsQualA);

        await db.SaveChangesAsync(ct);

        return new World(
            uid, deptId, secA.Id, secB.Id, qaUser.Id, qaRole.Id, analystA.Id, analystA2.Id, analystB.Id,
            sampleA.Id, sampleB.Id, reviewSampleA.Id, approvalSampleA.Id,
            retestA.Id, retestB.Id, oosA, oosB,
            orderA.Id, orderB.Id, uvOrderA.Id, incA.Id, incB.Id,
            lyoMatA.Id, lyoMatB.Id, dehydMatA.Id, refMatA.Id,
            mediaA.Id, mediaB.Id, evalA.Id, evalB.Id, chalA.Id, chalB.Id,
            cryoA.Id, cryoB.Id, eqA.Id, eqB.Id, autoclaveEqA.Id, uvEqA.Id, invA.Id, invB.Id, colA.Id, colB.Id,
            hplcMethodA.Id, hplcRunA.Id, solMasterA.Id, solPrepA.Id, wsQualA.Id, uvSpecA.Id);
    }

    public static IReadOnlyList<(string Family, Func<IUserSectionScopeService, int, SectionAccess, Task> Call)> All15ScopeFamilies(
        World w, bool useSectionA) =>
        new (string, Func<IUserSectionScopeService, int, SectionAccess, Task>)[]
        {
            ("EnsureTestOrderAccessAsync", (s, u, a) => s.EnsureTestOrderAccessAsync(u, useSectionA ? w.OrderAId : w.OrderBId, CancellationToken.None, a)),
            ("EnsureTestOrdersAccessAsync", (s, u, a) => s.EnsureTestOrdersAccessAsync(u, new[] { useSectionA ? w.OrderAId : w.OrderBId }, CancellationToken.None, a)),
            ("EnsureSampleAccessAsync", (s, u, a) => s.EnsureSampleAccessAsync(u, useSectionA ? w.SampleAId : w.SampleBId, CancellationToken.None, a)),
            ("EnsureSectionsAccessAsync", (s, u, a) => s.EnsureSectionsAccessAsync(u, new[] { useSectionA ? w.SectionAId : w.SectionBId }, CancellationToken.None, a)),
            ("EnsureIncubationAccessAsync", (s, u, a) => s.EnsureIncubationAccessAsync(u, useSectionA ? w.IncubationAId : w.IncubationBId, CancellationToken.None, a)),
            ("EnsurePathogenSampleAccessAsync", (s, u, a) => s.EnsurePathogenSampleAccessAsync(u, useSectionA ? w.SampleAId : w.SampleBId, CancellationToken.None, a)),
            ("EnsureMaterialAccessAsync", (s, u, a) => s.EnsureMaterialAccessAsync(u, useSectionA ? w.MaterialAId : w.MaterialBId, CancellationToken.None, a)),
            ("EnsureMediaAccessAsync", (s, u, a) => s.EnsureMediaAccessAsync(u, useSectionA ? w.MediaAId : w.MediaBId, CancellationToken.None, a)),
            ("EnsureCryovialAccessAsync", (s, u, a) => s.EnsureCryovialAccessAsync(u, useSectionA ? w.CryovialAId : w.CryovialBId, CancellationToken.None, a)),
            ("EnsureMediaEvaluationAccessAsync", (s, u, a) => s.EnsureMediaEvaluationAccessAsync(u, useSectionA ? w.MediaEvaluationAId : w.MediaEvaluationBId, CancellationToken.None, a)),
            ("EnsureMediaEvaluationChallengeAccessAsync", (s, u, a) => s.EnsureMediaEvaluationChallengeAccessAsync(u, useSectionA ? w.MediaChallengeAId : w.MediaChallengeBId, CancellationToken.None, a)),
            ("EnsureOosGroupAccessAsync", (s, u, a) => s.EnsureOosGroupAccessAsync(u, useSectionA ? w.OosGroupA : w.OosGroupB, CancellationToken.None, a)),
            ("EnsureEquipmentAccessAsync", (s, u, a) => s.EnsureEquipmentAccessAsync(u, useSectionA ? w.EquipmentAId : w.EquipmentBId, CancellationToken.None, a)),
            ("EnsureEquipmentInventoryAccessAsync", (s, u, a) => s.EnsureEquipmentInventoryAccessAsync(u, useSectionA ? w.EquipmentInventoryAId : w.EquipmentInventoryBId, CancellationToken.None, a)),
            ("EnsureColumnAccessAsync", (s, u, a) => s.EnsureColumnAccessAsync(u, useSectionA ? w.ColumnAId : w.ColumnBId, CancellationToken.None, a)),
        };
}
