using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class UniversalSpecificationsTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // 1. Parser ± cases
    [Theory]
    [InlineData("6.0 ± 0.5", 5.5, 6.5, null)]
    [InlineData("6.0 +/- 0.5", 5.5, 6.5, null)]
    [InlineData("6.0 +- 0.5", 5.5, 6.5, null)]
    [InlineData("100 ± 5%", 95, 105, "%")]
    [InlineData("100 +/- 5%", 95, 105, "%")]
    [InlineData("100 ± 5 %", 95, 105, "%")]
    [InlineData("50 ± 10%", 45, 55, "%")]
    [InlineData("6.0 ± 0.5 pH", 5.5, 6.5, "pH")]
    [InlineData("100 ± 5% mg", 95, 105, "mg")]
    public void SpecLimitParser_ParsesTargetWithToleranceCorrectly(string input, decimal expectedMin, decimal expectedMax, string? expectedUnit)
    {
        var parsed = SpecLimitParser.Parse(input);
        Assert.True(parsed.HasLimit);
        Assert.Equal(expectedMin, parsed.Min);
        Assert.Equal(expectedMax, parsed.Max);
        Assert.Equal(expectedUnit, parsed.Unit);
    }

    // 2. Evaluator for every numeric type incl. exclusive bounds and Percent tolerance
    [Fact]
    public void SpecificationEvaluator_Range_EvaluatesInclusiveAndExclusiveBounds()
    {
        // Inclusive (default)
        var specInc = new Specification
        {
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            LowerInclusive = true,
            UpperInclusive = true
        };
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specInc, 90m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specInc, 110m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specInc, 100m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specInc, 89.99m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specInc, 110.01m));

        // Exclusive lower
        var specExLower = new Specification
        {
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            LowerInclusive = false,
            UpperInclusive = true
        };
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specExLower, 90m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specExLower, 90.01m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specExLower, 110m));

        // Exclusive upper
        var specExUpper = new Specification
        {
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            LowerInclusive = true,
            UpperInclusive = false
        };
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specExUpper, 90m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specExUpper, 109.99m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specExUpper, 110m));

        // Exclusive both
        var specExBoth = new Specification
        {
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            LowerInclusive = false,
            UpperInclusive = false
        };
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specExBoth, 90m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specExBoth, 110m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specExBoth, 100m));
    }

    [Fact]
    public void SpecificationEvaluator_NotMoreThan_EvaluatesBounds()
    {
        var specInc = new Specification { LimitType = LimitType.NotMoreThan, UpperLimit = 100m, UpperInclusive = true };
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specInc, 100m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specInc, 50m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specInc, 100.01m));

        var specEx = new Specification { LimitType = LimitType.NotMoreThan, UpperLimit = 100m, UpperInclusive = false };
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specEx, 100m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specEx, 99.99m));
    }

    [Fact]
    public void SpecificationEvaluator_NotLessThan_EvaluatesBounds()
    {
        var specInc = new Specification { LimitType = LimitType.NotLessThan, LowerLimit = 90m, LowerInclusive = true };
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specInc, 90m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specInc, 95m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specInc, 89.99m));

        var specEx = new Specification { LimitType = LimitType.NotLessThan, LowerLimit = 90m, LowerInclusive = false };
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specEx, 90m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specEx, 90.01m));
    }

    [Fact]
    public void SpecificationEvaluator_TargetWithTolerance_EvaluatesAbsoluteAndPercent()
    {
        // Absolute: 100 ± 5 -> [95, 105]
        var specAbs = new Specification
        {
            LimitType = LimitType.TargetWithTolerance,
            Target = 100m,
            Tolerance = 5m,
            ToleranceMode = ToleranceMode.Absolute
        };
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specAbs, 95m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specAbs, 105m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specAbs, 100m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specAbs, 94.99m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specAbs, 105.01m));

        // Percent: 200 ± 5% -> 5% of 200 is 10 -> [190, 210]
        var specPct = new Specification
        {
            LimitType = LimitType.TargetWithTolerance,
            Target = 200m,
            Tolerance = 5m,
            ToleranceMode = ToleranceMode.Percent
        };
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specPct, 190m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specPct, 210m));
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(specPct, 200m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specPct, 189.99m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(specPct, 210.01m));
    }

    [Fact]
    public void SpecificationEvaluator_CountTiered_DelegatesToSpecLimitParser()
    {
        var spec = new Specification
        {
            LimitType = LimitType.CountTiered,
            AlertLimit = "10",
            ActionLimit = "50",
            SpecLimit = "100"
        };
        Assert.Equal("WithinLimits", SpecificationEvaluator.Evaluate(spec, 5m));
        Assert.Equal("AlertLimitExceeded", SpecificationEvaluator.Evaluate(spec, 15m));
        Assert.Equal("ActionLimitExceeded", SpecificationEvaluator.Evaluate(spec, 60m));
        Assert.Equal("OutOfSpecification", SpecificationEvaluator.Evaluate(spec, 150m));
    }

    [Theory]
    [InlineData(LimitType.Qualitative)]
    [InlineData(LimitType.PresenceAbsence)]
    [InlineData(LimitType.MultiStage)]
    public void SpecificationEvaluator_NonNumericTypes_ThrowsInvalidOperationException(LimitType limitType)
    {
        var spec = new Specification { LimitType = limitType };
        Assert.Throws<InvalidOperationException>(() => SpecificationEvaluator.Evaluate(spec, 50m));
    }

    // 3. Canonical text per type
    [Fact]
    public void SpecificationService_BuildCanonicalSpecLimit_FormatsAllTypesCorrectly()
    {
        var rangeSpec = new Specification { LimitType = LimitType.Range, LowerLimit = 90.0m, UpperLimit = 110.0m };
        Assert.Equal("90.0-110.0", SpecificationService.BuildCanonicalSpecLimit(rangeSpec));

        var nmtSpec = new Specification { LimitType = LimitType.NotMoreThan, UpperLimit = 100m };
        Assert.Equal("NMT 100", SpecificationService.BuildCanonicalSpecLimit(nmtSpec));

        var nltSpec = new Specification { LimitType = LimitType.NotLessThan, LowerLimit = 90m };
        Assert.Equal("NLT 90", SpecificationService.BuildCanonicalSpecLimit(nltSpec));

        var tolAbsSpec = new Specification { LimitType = LimitType.TargetWithTolerance, Target = 6.0m, Tolerance = 0.5m, ToleranceMode = ToleranceMode.Absolute };
        Assert.Equal("6.0 ± 0.5", SpecificationService.BuildCanonicalSpecLimit(tolAbsSpec));

        var tolPctSpec = new Specification { LimitType = LimitType.TargetWithTolerance, Target = 100m, Tolerance = 5m, ToleranceMode = ToleranceMode.Percent };
        Assert.Equal("100 ± 5%", SpecificationService.BuildCanonicalSpecLimit(tolPctSpec));

        var qualSpec = new Specification { LimitType = LimitType.Qualitative, ExpectedResultText = "Clear liquid" };
        Assert.Equal("Clear liquid", SpecificationService.BuildCanonicalSpecLimit(qualSpec));

        var presSpec = new Specification { LimitType = LimitType.PresenceAbsence, ExpectedState = ExpectedPresence.Presence };
        Assert.Equal("Present", SpecificationService.BuildCanonicalSpecLimit(presSpec));

        var absSpec = new Specification { LimitType = LimitType.PresenceAbsence, ExpectedState = ExpectedPresence.Absence };
        Assert.Equal("Absent", SpecificationService.BuildCanonicalSpecLimit(absSpec));

        var msSpec = new Specification { LimitType = LimitType.MultiStage };
        Assert.Equal(string.Empty, SpecificationService.BuildCanonicalSpecLimit(msSpec));
    }

    [Fact]
    public void SpecificationService_ApplyCanonicalSpecLimit_ClearsAlertActionAndDilutionFactorForNonCountTiered()
    {
        var spec = new Specification
        {
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            AlertLimit = "95-105",
            ActionLimit = "92-108",
            DilutionFactor = 10m
        };

        SpecificationService.ApplyCanonicalSpecLimit(spec);

        Assert.Equal("90-110", spec.SpecLimit);
        Assert.Equal(string.Empty, spec.AlertLimit);
        Assert.Equal(string.Empty, spec.ActionLimit);
        Assert.Null(spec.DilutionFactor);
    }

    // 4. Validation failures
    [Fact]
    public async Task SpecificationService_ValidationFailures_EnforcesAllRules()
    {
        using var db = NewDb();
        var item = new Item { Id = 1, Name = "Item 1", Code = "ITM-1" };
        db.Items.Add(item);
        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = "HPLC_ASSAY", DisplayName = "HPLC Assay" });
        db.Specifications.Add(new Specification
        {
            ItemId = item.Id,
            TestCode = "HPLC_ASSAY",
            ParameterName = "ExistingParam",
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        });
        await db.SaveChangesAsync();

        var service = new SpecificationService(db);

        // Lower > Upper
        var invalidRange = new Specification
        {
            ItemId = item.Id,
            TestCode = "HPLC_ASSAY",
            ParameterName = "Param1",
            LimitType = LimitType.Range,
            LowerLimit = 110m,
            UpperLimit = 90m
        };
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(invalidRange));
        Assert.Contains("Lower limit cannot be greater than upper limit", ex1.Message);

        // Missing fields (Range missing upper)
        var missingUpper = new Specification
        {
            ItemId = item.Id,
            TestCode = "HPLC_ASSAY",
            ParameterName = "Param2",
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = null
        };
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(missingUpper));
        Assert.Contains("Lower limit and upper limit are required", ex2.Message);

        // Tolerance <= 0
        var invalidTol = new Specification
        {
            ItemId = item.Id,
            TestCode = "HPLC_ASSAY",
            ParameterName = "Param3",
            LimitType = LimitType.TargetWithTolerance,
            Target = 100m,
            Tolerance = 0m,
            ToleranceMode = ToleranceMode.Absolute
        };
        var ex3 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(invalidTol));
        Assert.Contains("Tolerance must be greater than zero", ex3.Message);

        // DilutionFactor on non-CountTiered
        var dfOnRange = new Specification
        {
            ItemId = item.Id,
            TestCode = "HPLC_ASSAY",
            ParameterName = "Param4",
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m,
            DilutionFactor = 10m
        };
        var ex4 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(dfOnRange));
        Assert.Contains("Dilution factor is only allowed for Count-Tiered", ex4.Message);

        // Duplicate parameter name
        var duplicateParam = new Specification
        {
            ItemId = item.Id,
            TestCode = "HPLC_ASSAY",
            ParameterName = "ExistingParam",
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        };
        var ex5 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(duplicateParam));
        Assert.Contains("already exists", ex5.Message);

        // Test not assigned to item
        var unassignedTest = new Specification
        {
            ItemId = item.Id,
            TestCode = "UNASSIGNED_TEST",
            ParameterName = "Param5",
            LimitType = LimitType.CountTiered,
            SpecLimit = "100"
        };
        var ex6 = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(unassignedTest));
        Assert.Contains("is not assigned to item", ex6.Message);
    }

    // 5. PrimaryAsync ordering: lowest DisplayOrder then Id
    [Fact]
    public async Task SpecificationLookup_PrimaryAsync_ReturnsLowestDisplayOrderThenId()
    {
        using var db = NewDb();
        const int itemId = 42;
        const string testCode = "ASSAY";

        var spec1 = new Specification { ItemId = itemId, TestCode = testCode, ParameterName = "P1", DisplayOrder = 2 };
        var spec2 = new Specification { ItemId = itemId, TestCode = testCode, ParameterName = "P2", DisplayOrder = 0 };
        var spec3 = new Specification { ItemId = itemId, TestCode = testCode, ParameterName = "P3", DisplayOrder = 1 };
        var spec4 = new Specification { ItemId = itemId, TestCode = testCode, ParameterName = "P4", DisplayOrder = 0 };

        db.Specifications.AddRange(spec1, spec2, spec3, spec4);
        await db.SaveChangesAsync();

        var primary = await SpecificationLookup.PrimaryAsync(db, itemId, testCode);
        Assert.NotNull(primary);
        // spec2 and spec4 have DisplayOrder = 0; spec2 has lower Id than spec4
        Assert.Equal(spec2.Id, primary.Id);
        Assert.Equal("P2", primary.ParameterName);
    }

    // 6. Old-style CountTiered create payload still works unchanged
    [Fact]
    public async Task MasterDataController_OldStyleCountTieredPayload_WorksUnchanged()
    {
        using var db = NewDb();
        var item = new Item { Name = "Old Item", Code = "OLD-1" };
        db.Items.Add(item);
        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", WorkflowType = WorkflowType.CountTest });
        db.SampleTests.Add(new SampleTest { Item = item, TestCode = "TAMC", DisplayName = "Total Aerobic Microbial Count" });
        await db.SaveChangesAsync();

        var controller = new MasterDataController(
            db,
            new EquipmentConfigurationService(db),
            TestServiceFactory.MediaProduct(db),
            TestServiceFactory.MediaIncubationCondition(db),
            new UserSectionScopeService(db),
            new ChromatographyColumnService(db, new UserSectionScopeService(db)),
            new SpecificationService(db));

        // Task 9 - CreateSpecification/GetSpecifications now resolve the
        // current user's lab scope; a System Administrator's scope is null
        // (unrestricted), so this pre-existing test needn't seed a section
        // for the un-sectioned TestDefinition above.
        var adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        db.Roles.Add(adminRole);
        var adminUser = new User { Username = "admin_" + Guid.NewGuid().ToString("N")[..6], FullName = "Sys Admin", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminUser.Id.ToString()) },
                        "TestAuth"))
            }
        };

        // Old-style request (only original positional args)
        var oldRequest = new CreateSpecificationRequest(
            ItemId: item.Id,
            TestCode: "TAMC",
            AlertLimit: "10",
            ActionLimit: "50",
            SpecLimit: "100",
            Unit: "CFU/g",
            DilutionFactor: 10m);

        var actionResult = await controller.CreateSpecification(oldRequest);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var envelope = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var createdSpec = Assert.IsType<Specification>(envelope.Data);

        Assert.Equal(LimitType.CountTiered, createdSpec.LimitType);
        Assert.Equal("Total Aerobic Microbial Count", createdSpec.ParameterName); // backfilled from TestDefinition.DisplayName
        Assert.Equal("10", createdSpec.AlertLimit);
        Assert.Equal("50", createdSpec.ActionLimit);
        Assert.Equal("100", createdSpec.SpecLimit);
        Assert.Equal("CFU/g", createdSpec.Unit);
        Assert.Equal(10m, createdSpec.DilutionFactor);

        // GET includes stages and returns the spec
        var getResult = await controller.GetSpecifications(item.Id);
        var getOk = Assert.IsType<OkObjectResult>(getResult);
        var getEnvelope = Assert.IsType<ApiResponse<object>>(getOk.Value);
        var list = Assert.IsAssignableFrom<IEnumerable<SpecificationRowDto>>(getEnvelope.Data);
        var retrieved = Assert.Single(list);
        Assert.Equal(createdSpec.Id, retrieved.Id);
        Assert.NotNull(retrieved.Stages);
    }
}
