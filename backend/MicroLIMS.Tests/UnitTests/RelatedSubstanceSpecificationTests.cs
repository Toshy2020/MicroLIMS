using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;
using Base = MicroLIMS.Tests.UnitTests.HplcMethodServiceTests;

namespace MicroLIMS.Tests.UnitTests;

// Related-substances specification rows (Plan 3 task 2).
public class RelatedSubstanceSpecificationTests
{
    private record Arr(SpecificationMasterDataService Svc, Item Item, string Code, int AnalyteId, int ImpurityDId,
        int OtherMethodImpurityId, string AssayCode, int AssayAnalyteId, int UserId);

    private static async Task<Arr> ArrangeAsync(MicroLimsDbContext db)
    {
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var methods = TestServiceFactory.HplcMethod(db);

        SaveHplcMethodRequest Rs(string abbr, string impurity) =>
            Base.Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { new(null, "Principal", 230m, standard.Id, 0m, 0m, 5) },
                name: "RS " + abbr, abbreviation: abbr) with
            {
                ResultMode = HplcResultMode.RelatedSubstances, ReferenceSolutionPercent = 0.1m, DisregardLimitPercent = 0.05m,
                Impurities = new() { new(null, impurity, 0.15m, 0.7m) },
            };

        var rs = await methods.CreateAsync(Rs("RS-01", "Impurity D"), userId);
        var other = await methods.CreateAsync(Rs("RS-02", "Impurity Z"), userId);
        var assay = await methods.CreateAsync(
            Base.Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { Base.AnalyteInput(standard.Id) }, abbreviation: "AS-01"), userId);

        var master = TestServiceFactory.TestDefinitionMaster(db);
        CreateTestDefinitionRequest Def(string code, int methodId) => new(
            Code: code, DisplayName: code, SectionId: section.Id, WorkflowType: WorkflowType.HplcMethodAssay,
            EquationType: EquationType.HplcMethodAssay, RequiresSystemSuitability: true, MethodAbbreviation: code, HplcMethodId: methodId);
        var rsDef = await master.CreateTestDefinitionAsync(userId, Def("RS-T1", rs.Id));
        var assayDef = await master.CreateTestDefinitionAsync(userId, Def("AS-T1", assay.Id));

        var item = new Item
        {
            Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6], Name = "Item 1",
            AssignedTests = { new SampleTest { TestCode = rsDef.Code, DisplayName = rsDef.Code }, new SampleTest { TestCode = assayDef.Code, DisplayName = assayDef.Code } },
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return new Arr(new SpecificationMasterDataService(db, new UserSectionScopeService(db)), item, rsDef.Code,
            rs.Analytes[0].Id, rs.Impurities[0].Id, other.Impurities[0].Id, assayDef.Code, assay.Analytes[0].Id, userId);
    }

    private static CreateSpecificationRequest Row(Arr a, string name, RelatedSubstanceRow? kind, decimal limit,
        int? impurityId = null, LimitType limitType = LimitType.NotMoreThan, ResultBasis basis = ResultBasis.PercentAsIs,
        ProductionStageRole? stage = null, string? grade = null) =>
        new(ItemId: a.Item.Id, TestCode: a.Code, ParameterName: name, LimitType: limitType, UpperLimit: limit, Unit: "%",
            ResultBasis: basis, HplcMethodAnalyteId: a.AnalyteId, RelatedSubstanceRow: kind, HplcMethodImpurityId: impurityId,
            ProductionStageRole: stage, Grade: grade);

    private static async Task AssertThrowsAsync(string message, Func<Task> act) =>
        Assert.Equal(message, (await Assert.ThrowsAsync<InvalidOperationException>(act)).Message);

    [Fact]
    public async Task RelatedSubstancesSpec_ValidRows_Save()
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);

        var named = await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Impurity D", RelatedSubstanceRow.Named, 0.15m, a.ImpurityDId));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Any unspecified impurity", RelatedSubstanceRow.UnspecifiedEach, 0.10m));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Total impurities", RelatedSubstanceRow.Total, 0.5m));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Total of others", RelatedSubstanceRow.TotalOthers, 1.0m));

        Assert.Equal(4, await db.Specifications.CountAsync(s => s.HplcMethodAnalyteId == a.AnalyteId));
        Assert.Equal(RelatedSubstanceRow.Named, named.RelatedSubstanceRow);
        Assert.Equal(a.ImpurityDId, named.HplcMethodImpurityId);
        Assert.Equal(ResultBasis.PercentAsIs, named.ResultBasis);
    }

    [Theory]
    [InlineData("kind-missing", "Choose the related-substance row: named impurity, any unspecified impurity, total, or total of others.")]
    [InlineData("named-no-impurity", "Choose the impurity for this row.")]
    [InlineData("other-method", "The impurity does not belong to this test's HPLC method.")]
    [InlineData("non-named-with-impurity", "Only a named-impurity row names an impurity.")]
    [InlineData("limit-type", "Related-substance limits are 'not more than' limits.")]
    [InlineData("basis", "Related-substance rows report % (as is).")]
    [InlineData("label-claim", "Related-substance rows have no label claim.")]
    [InlineData("limit-zero", "Enter the limit (above 0 %).")]
    [InlineData("limit-null", "Enter the limit (above 0 %).")]
    [InlineData("undefined-kind", "Choose the related-substance row: named impurity, any unspecified impurity, total, or total of others.")]
    public async Task RelatedSubstancesSpec_Refusals(string field, string message)
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);

        var req = field switch
        {
            "kind-missing" => Row(a, "Row", null, 0.1m),
            "named-no-impurity" => Row(a, "Row", RelatedSubstanceRow.Named, 0.1m),
            "other-method" => Row(a, "Row", RelatedSubstanceRow.Named, 0.1m, a.OtherMethodImpurityId),
            "non-named-with-impurity" => Row(a, "Row", RelatedSubstanceRow.Total, 0.1m, a.ImpurityDId),
            "limit-type" => Row(a, "Row", RelatedSubstanceRow.Total, 0.1m, limitType: LimitType.Range) with { LowerLimit = 0m },
            "basis" => Row(a, "Row", RelatedSubstanceRow.Total, 0.1m, basis: ResultBasis.PercentLabelClaim),
            "label-claim" => Row(a, "Row", RelatedSubstanceRow.Total, 0.1m) with { LabelClaim = 10m, LabelClaimUnit = "mg" },
            "limit-zero" => Row(a, "Row", RelatedSubstanceRow.Total, 0m),
            "limit-null" => Row(a, "Row", RelatedSubstanceRow.Total, 0.1m) with { UpperLimit = null },
            "undefined-kind" => Row(a, "Row", (RelatedSubstanceRow)99, 0.1m),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        await AssertThrowsAsync(message, () => a.Svc.CreateSpecificationAsync(a.UserId, req));
    }

    [Theory]
    [InlineData("same-kind", "A total impurities row already exists for this test, stage and grade.")]
    [InlineData("same-named", "A named impurity 'Impurity D' row already exists for this test, stage and grade.")]
    public async Task RelatedSubstancesSpec_DuplicateKindForSameStageAndGrade_Refused(string field, string message)
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);
        var first = field == "same-named"
            ? Row(a, "Impurity D", RelatedSubstanceRow.Named, 0.15m, a.ImpurityDId)
            : Row(a, "Total impurities", RelatedSubstanceRow.Total, 0.5m);
        await a.Svc.CreateSpecificationAsync(a.UserId, first);

        var second = first with { ParameterName = "Renamed row" };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => a.Svc.CreateSpecificationAsync(a.UserId, second));
        Assert.Equal(message, ex.Message);

        // A different kind, or another stage, is not a duplicate.
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Total of others", RelatedSubstanceRow.TotalOthers, 1.0m));
        await a.Svc.CreateSpecificationAsync(a.UserId, second with { Grade = "USP" });
    }

    [Fact]
    public async Task RelatedSubstancesSpec_SharedParameterName_AllFourRowsSave()
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);

        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Impurities", RelatedSubstanceRow.Named, 0.15m, a.ImpurityDId));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Impurities", RelatedSubstanceRow.UnspecifiedEach, 0.10m));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Impurities", RelatedSubstanceRow.Total, 0.5m));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Impurities", RelatedSubstanceRow.TotalOthers, 1.0m));

        Assert.Equal(4, await db.Specifications.CountAsync(s => s.ParameterName == "Impurities"));
    }

    [Fact]
    public async Task RelatedSubstancesSpec_BlankParameterName_DefaultsToTheRowKind()
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);

        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "", RelatedSubstanceRow.Named, 0.15m, a.ImpurityDId));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, " ", RelatedSubstanceRow.UnspecifiedEach, 0.10m));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "", RelatedSubstanceRow.Total, 0.5m));
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "", RelatedSubstanceRow.TotalOthers, 1.0m));

        var names = await db.Specifications.Select(s => s.ParameterName).ToListAsync();
        Assert.Equal(new[] { "Any unspecified impurity", "Impurity D", "Total impurities", "Total of others" }, names.OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task RelatedSubstanceFields_OnANonHplcTest_Refused()
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);
        db.TestDefinitions.Add(new TestDefinition { Code = "PLAIN-T1", DisplayName = "Plain", SectionId = (await db.DocumentSections.FirstAsync()).Id, WorkflowType = WorkflowType.Measurement });
        a.Item.AssignedTests.Add(new SampleTest { TestCode = "PLAIN-T1", DisplayName = "Plain" });
        await db.SaveChangesAsync();

        var req = new CreateSpecificationRequest(ItemId: a.Item.Id, TestCode: "PLAIN-T1", ParameterName: "Plain",
            LimitType: LimitType.NotMoreThan, UpperLimit: 1m, RelatedSubstanceRow: RelatedSubstanceRow.Total);

        await AssertThrowsAsync("Related-substance rows belong to related-substances methods only.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, req));
    }

    [Fact]
    public async Task AssayModeSpec_RefusesRelatedSubstanceFields()
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);
        var req = new CreateSpecificationRequest(ItemId: a.Item.Id, TestCode: a.AssayCode, ParameterName: "Assay",
            LimitType: LimitType.Range, LowerLimit: 90m, UpperLimit: 110m, ResultBasis: ResultBasis.PercentLabelClaim,
            HplcMethodAnalyteId: a.AssayAnalyteId, RelatedSubstanceRow: RelatedSubstanceRow.Total);

        await AssertThrowsAsync("Related-substance rows belong to related-substances methods only.",
            () => a.Svc.CreateSpecificationAsync(a.UserId, req));
    }

    [Fact]
    public async Task Update_RemovingImpurityUsedBySpecification_Refused()
    {
        await using var db = TestDb();
        var a = await ArrangeAsync(db);
        await a.Svc.CreateSpecificationAsync(a.UserId, Row(a, "Impurity D", RelatedSubstanceRow.Named, 0.15m, a.ImpurityDId));

        var methods = TestServiceFactory.HplcMethod(db);
        var method = await db.HplcMethods.Include(m => m.Analytes).Include(m => m.Impurities).FirstAsync(m => m.Analytes.Any(x => x.Id == a.AnalyteId));
        var current = await methods.GetByIdAsync(method.Id, a.UserId);
        var req = Base.Req(current.DiluentSolutionId, new() { new("A", current.MobilePhases[0].SolutionMasterId, null) },
            new() { new(a.AnalyteId, "Principal", 230m, current.Analytes[0].StandardEntryId, 0m, 0m, 5) },
            name: current.Name, abbreviation: current.Abbreviation, reason: "drop") with
        {
            ResultMode = HplcResultMode.RelatedSubstances, ReferenceSolutionPercent = 0.1m, DisregardLimitPercent = 0.05m,
            Impurities = new() { new(null, "Impurity Q", 0.5m, null) },
        };

        await AssertThrowsAsync("Impurity Impurity D is used by a specification and cannot be removed.",
            () => methods.UpdateAsync(method.Id, req, a.UserId));
        Assert.True(await db.HplcMethodImpurities.AnyAsync(i => i.Id == a.ImpurityDId));

        // Keeping it (matched by Id) is fine.
        var ok = await methods.UpdateAsync(method.Id, req with
        {
            Impurities = new() { new(a.ImpurityDId, "Impurity D", 0.16m, 0.7m), new(null, "Impurity Q", 0.5m, null) },
        }, a.UserId);
        Assert.Equal(2, ok.Impurities.Count);
    }

    private static MicroLimsDbContext TestDb() => Base.NewDb();
}
