using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MoistureRoleSpecificationTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // An item assigned the assay titration test, plus any extra tests (code, role).
    private static async Task<(MicroLimsDbContext db, Item item)> SetupAsync(params (string code, WorkflowType wf, MoistureRole role)[] extra)
    {
        var db = NewDb();
        var item = new Item { Code = "RM-1", Name = "Raw material", IsActive = true };
        db.Items.Add(item);
        db.TestDefinitions.Add(new TestDefinition { Code = "ASSAY-TIT", DisplayName = "Assay", WorkflowType = WorkflowType.Titration, IsActive = true });
        foreach (var (code, wf, role) in extra)
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, WorkflowType = wf, MoistureRole = role, IsActive = true });
        await db.SaveChangesAsync();
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "ASSAY-TIT" });
        foreach (var (code, _, _) in extra)
            db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = code });
        await db.SaveChangesAsync();
        return (db, item);
    }

    private static Specification TitrationSpec(Item item, ResultBasis basis) => new()
    {
        ItemId = item.Id,
        TestCode = "ASSAY-TIT",
        ParameterName = "Assay",
        LimitType = LimitType.Range,
        LowerLimit = 98m,
        UpperLimit = 102m,
        Unit = "%",
        ResultBasis = basis
    };

    [Fact]
    public async Task DriedBasisTitrationSpec_NeedsAssignedLodTest()
    {
        var (db, item) = await SetupAsync();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentDriedBasis)));
        Assert.Contains("Assign a loss on drying test to this item before using a dried basis.", ex.Message);
    }

    [Fact]
    public async Task AnhydrousBasisTitrationSpec_NeedsAssignedWaterTest()
    {
        // A LOD test does not satisfy the anhydrous basis.
        var (db, item) = await SetupAsync(("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying));
        using var _ = db;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentAnhydrousBasis)));
        Assert.Contains("Assign a water (Karl Fischer) test to this item before using an anhydrous basis.", ex.Message);
    }

    [Fact]
    public async Task DriedBasisTitrationSpec_SavesWhenLodAssigned_WithoutLodSpecRow()
    {
        var (db, item) = await SetupAsync(("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying));
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentDriedBasis));
    }

    [Fact]
    public async Task AnhydrousBasisTitrationSpec_SavesWhenWaterAssigned()
    {
        var (db, item) = await SetupAsync(("KF", WorkflowType.Titration, MoistureRole.Water));
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentAnhydrousBasis));
    }

    [Fact]
    public async Task AnhydrousBasisTitrationSpec_OnTheWaterTestItself_IsRefused()
    {
        // The only water test on the item is the test being specified: it cannot satisfy its own prerequisite.
        var (db, item) = await SetupAsync(("KF", WorkflowType.Titration, MoistureRole.Water));
        using var _ = db;
        var spec = TitrationSpec(item, ResultBasis.PercentAnhydrousBasis);
        spec.TestCode = "KF";
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => new SpecificationService(db).ValidateAsync(spec));
        Assert.Contains("Assign a water (Karl Fischer) test to this item", ex.Message);
    }

    [Fact]
    public async Task DriedBasisTitrationSpec_InactiveLodTest_IsRefused()
    {
        var (db, item) = await SetupAsync(("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying));
        using var _ = db;
        (await db.TestDefinitions.SingleAsync(t => t.Code == "LOD")).IsActive = false;
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentDriedBasis)));
        Assert.Contains("Assign a loss on drying test to this item", ex.Message);
    }

    [Fact]
    public async Task WaterRoleChange_OnTitrationTest_NeedsReasonAndIsAudited()
    {
        using var db = NewDb();
        var s = TitrationScenario.Seed(db, new()
        {
            Unit = TitrantStrengthUnit.MgWaterPerMl, Nominal = 5.0m,
            Tweak = t =>
            {
                t.TitrationType = TitrationType.KarlFischer; t.TitrationEquivalencyFactor = null; t.TitrationEndpoint = TitrationEndpoint.Potentiometric;
                t.TitrationIndicatorEntryId = null; t.TitrationIndicator = null;
            },
        });
        var svc = TestServiceFactory.TestDefinitionMaster(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.UpdateTestDefinitionAsync(s.UserId, s.Test.Id, new UpdateTestDefinitionRequest(s.Test.Code, s.Test.DisplayName, MoistureRole: MoistureRole.Water)));
        Assert.Contains("reason for change", ex.Message);
    }

    [Fact]
    public async Task TitrationTest_MixingDriedAndAnhydrousBasisRows_IsRefused()
    {
        var (db, item) = await SetupAsync(
            ("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying),
            ("KF", WorkflowType.Titration, MoistureRole.Water));
        using var _ = db;
        var dried = TitrationSpec(item, ResultBasis.PercentDriedBasis);
        db.Specifications.Add(dried);
        await db.SaveChangesAsync();

        var anhydrous = TitrationSpec(item, ResultBasis.PercentAnhydrousBasis);
        anhydrous.ParameterName = "Assay (anhydrous)";
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => new SpecificationService(db).ValidateAsync(anhydrous));
        Assert.Contains("One titration cannot report on both dried and anhydrous basis.", ex.Message);

        // Re-validating the existing dried row (an edit) is not blocked by itself.
        await new SpecificationService(db).ValidateAsync(dried);
    }

    [Fact]
    public async Task AsIsTitrationSpec_NeedsNoMoistureTest()
    {
        var (db, item) = await SetupAsync();
        using var _ = db;
        await new SpecificationService(db).ValidateAsync(TitrationSpec(item, ResultBasis.PercentAsIs));
    }

    private static async Task<(MicroLimsDbContext db, Item item, HplcMethodAnalyte analyte, TestDefinition testDef)> SetupHplcAsync(
        params (string code, WorkflowType wf, MoistureRole role)[] extra)
    {
        var db = NewDb();
        var item = new Item { Code = "RM-1", Name = "Raw material", IsActive = true };
        db.Items.Add(item);

        var method = new HplcMethod { Name = "HPLC Method", Abbreviation = "HPLC", ResultMode = HplcResultMode.Assay, IsActive = true };
        db.HplcMethods.Add(method);
        var analyte = new HplcMethodAnalyte { HplcMethod = method, Name = "Analyte 1", DisplayOrder = 1, WavelengthNm = 254 };
        db.HplcMethodAnalytes.Add(analyte);

        var testDef = new TestDefinition
        {
            Code = "ASSAY-HPLC",
            DisplayName = "Assay HPLC",
            WorkflowType = WorkflowType.HplcMethodAssay,
            EquationType = EquationType.HplcMethodAssay,
            HplcMethod = method,
            HplcMethodId = method.Id,
            IsActive = true
        };
        db.TestDefinitions.Add(testDef);

        foreach (var (code, wf, role) in extra)
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, WorkflowType = wf, MoistureRole = role, IsActive = true });
        await db.SaveChangesAsync();

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "ASSAY-HPLC" });
        foreach (var (code, _, _) in extra)
            db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = code });
        await db.SaveChangesAsync();

        return (db, item, analyte, testDef);
    }

    private static Specification HplcSpec(Item item, HplcMethodAnalyte analyte, ResultBasis basis) => new()
    {
        ItemId = item.Id,
        TestCode = "ASSAY-HPLC",
        ParameterName = "Assay",
        HplcMethodAnalyteId = analyte.Id,
        LimitType = LimitType.Range,
        LowerLimit = 98m,
        UpperLimit = 102m,
        Unit = "%",
        ResultBasis = basis
    };

    [Fact]
    public async Task HplcSpec_DriedBasis_NeedsLodTest()
    {
        var (db, item, analyte, _) = await SetupHplcAsync();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SpecificationService(db).ValidateAsync(HplcSpec(item, analyte, ResultBasis.PercentDriedBasis)));
        Assert.Contains("Assign a loss on drying test to this item before using a dried basis.", ex.Message);

        var (db2, item2, analyte2, _) = await SetupHplcAsync(("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying));
        using var _2 = db2;
        await new SpecificationService(db2).ValidateAsync(HplcSpec(item2, analyte2, ResultBasis.PercentDriedBasis));
    }

    [Fact]
    public async Task HplcSpec_LabelClaimAndContentBasis_Refused()
    {
        var (db, item, analyte, _) = await SetupHplcAsync(("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying));
        using var _ = db;
        var claimSpec = HplcSpec(item, analyte, ResultBasis.PercentLabelClaim);
        db.Specifications.Add(claimSpec);
        await db.SaveChangesAsync();

        var driedSpec = HplcSpec(item, analyte, ResultBasis.PercentDriedBasis);
        driedSpec.ParameterName = "Assay (dried)";
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SpecificationService(db).ValidateAsync(driedSpec));
        Assert.Contains("An HPLC analyte reports either % of label claim or a content basis, not both.", ex.Message);

        var asIsSpec = HplcSpec(item, analyte, ResultBasis.PercentAsIs);
        asIsSpec.ParameterName = "Assay (as-is)";
        var ex2 = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SpecificationService(db).ValidateAsync(asIsSpec));
        Assert.Contains("An HPLC analyte reports either % of label claim or a content basis, not both.", ex2.Message);
    }

    [Fact]
    public async Task HplcTest_MixingDriedAndAnhydrousBasisRows_IsRefused()
    {
        var (db, item, analyte, _) = await SetupHplcAsync(
            ("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying),
            ("KF", WorkflowType.Titration, MoistureRole.Water));
        using var _ = db;
        var analyte2 = new HplcMethodAnalyte { HplcMethodId = analyte.HplcMethodId, Name = "Analyte 2", DisplayOrder = 2, WavelengthNm = 254 };
        db.HplcMethodAnalytes.Add(analyte2);
        db.Specifications.Add(HplcSpec(item, analyte, ResultBasis.PercentDriedBasis));
        await db.SaveChangesAsync();

        var anhydrous = HplcSpec(item, analyte2, ResultBasis.PercentAnhydrousBasis);
        anhydrous.ParameterName = "Assay 2";
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => new SpecificationService(db).ValidateAsync(anhydrous));
        Assert.Equal("One HPLC test cannot report on both dried and anhydrous basis.", ex.Message);
    }

    [Fact]
    public async Task HplcSpec_BadBasis_MessageListsEveryAllowedBasis()
    {
        var (db, item, analyte, _) = await SetupHplcAsync();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => new SpecificationService(db).ValidateAsync(HplcSpec(item, analyte, ResultBasis.Ppm)));
        Assert.Equal("Result basis must be assay % of label claim, amount per unit, % as is, % dried basis or % anhydrous basis.", ex.Message);
    }

    [Fact]
    public async Task HplcSpec_BasisConflicts_AreScopedPerGradeAndStage()
    {
        var (db, item, analyte, _) = await SetupHplcAsync(
            ("LOD", WorkflowType.Gravimetric, MoistureRole.LossOnDrying),
            ("KF", WorkflowType.Titration, MoistureRole.Water));
        using var _ = db;
        item.Category = SampleCategory.FinishedProduct; // stage-specific rows exist only on finished products
        var claim = HplcSpec(item, analyte, ResultBasis.PercentLabelClaim);
        claim.Grade = "USP";
        db.Specifications.Add(claim);
        var dried = HplcSpec(item, analyte, ResultBasis.PercentDriedBasis);
        dried.ParameterName = "Assay (dried)";
        dried.Grade = "USP";
        dried.ProductionStageRole = ProductionStageRole.Bulk;
        db.Specifications.Add(dried);
        await db.SaveChangesAsync();
        var service = new SpecificationService(db);

        // Another grade, or another stage, is a different specification set: no conflict either way.
        var otherGrade = HplcSpec(item, analyte, ResultBasis.PercentDriedBasis);
        otherGrade.ParameterName = "Assay (dried, in-house)";
        otherGrade.Grade = "In-house";
        await service.ValidateAsync(otherGrade);

        var otherStage = HplcSpec(item, analyte, ResultBasis.PercentAnhydrousBasis);
        otherStage.ParameterName = "Assay (anhydrous, finished)";
        otherStage.Grade = "USP";
        otherStage.ProductionStageRole = ProductionStageRole.Finished;
        await service.ValidateAsync(otherStage);

        // Same grade and stage still conflicts.
        var sameScope = HplcSpec(item, analyte, ResultBasis.PercentAsIs);
        sameScope.ParameterName = "Assay (as is)";
        sameScope.Grade = "usp"; // case-insensitive, like the duplicate check
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ValidateAsync(sameScope));
        Assert.Equal("An HPLC analyte reports either % of label claim or a content basis, not both.", ex.Message);
        var anhydrousSameScope = HplcSpec(item, analyte, ResultBasis.PercentAnhydrousBasis);
        anhydrousSameScope.ParameterName = "Assay (anhydrous)";
        anhydrousSameScope.Grade = "USP";
        anhydrousSameScope.ProductionStageRole = ProductionStageRole.Bulk;
        var ex2 = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ValidateAsync(anhydrousSameScope));
        Assert.Equal("One HPLC test cannot report on both dried and anhydrous basis.", ex2.Message);
    }
}
