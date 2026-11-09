using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Tests.UnitTests;
using Xunit;
using Base = MicroLIMS.Tests.UnitTests.HplcMethodServiceTests;

namespace MicroLIMS.Tests.IntegrationTests;

// Related-substance spec rows on Npgsql: the null-safe kind/impurity key, the unique index that allows
// rows to share a parameter name, and the restrict FK from a row to its impurity.
[Collection("PostgresDatabaseCollection")]
public class RelatedSubstanceSpecPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public RelatedSubstanceSpecPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task Rows_SaveWithSharedName_DuplicateKindRefused_ImpurityDeleteRestricted()
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var userId = _fixture.SeededUserId;

        await using var db = _fixture.CreateDbContext();
        var section = TestServiceFactory.EnsureMicroSection(db);
        TestServiceFactory.AssignUserToMicroSection(db, userId);
        var diluent = await Base.AddSolutionAsync(db, section.Id, "Diluent " + tag, SolutionType.Diluent);
        var mp = await Base.AddSolutionAsync(db, section.Id, "Mobile " + tag, SolutionType.MobilePhase);
        var standard = await Base.AddEntryAsync(db, section.Id, "STD-" + tag);

        var method = await TestServiceFactory.HplcMethod(db).CreateAsync(
            Base.Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { new(null, "Principal", 230m, standard.Id, 0m, 0m, 5) },
                name: "RS " + tag, abbreviation: "RS-" + tag, sectionId: section.Id) with
            {
                ResultMode = HplcResultMode.RelatedSubstances, ReferenceSolutionPercent = 0.1m, DisregardLimitPercent = 0.05m,
                Impurities = new() { new(null, "Impurity D", 0.15m, 0.7m) },
            }, userId);
        var def = await TestServiceFactory.TestDefinitionMaster(db).CreateTestDefinitionAsync(userId, new CreateTestDefinitionRequest(
            Code: "RS-" + tag, DisplayName: "RS " + tag, SectionId: section.Id, WorkflowType: WorkflowType.HplcMethodAssay,
            EquationType: EquationType.HplcMethodAssay, RequiresSystemSuitability: true, MethodAbbreviation: "RS-" + tag, HplcMethodId: method.Id));
        var item = new Item { Code = "ITEM-" + tag, Name = "Item " + tag, AssignedTests = { new SampleTest { TestCode = def.Code, DisplayName = def.Code } } };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var svc = new SpecificationMasterDataService(db, new UserSectionScopeService(db));
        CreateSpecificationRequest Row(RelatedSubstanceRow kind, decimal limit, int? impurityId = null) =>
            new(ItemId: item.Id, TestCode: def.Code, ParameterName: "Impurities", LimitType: LimitType.NotMoreThan, UpperLimit: limit,
                Unit: "%", ResultBasis: ResultBasis.PercentAsIs, HplcMethodAnalyteId: method.Analytes[0].Id,
                RelatedSubstanceRow: kind, HplcMethodImpurityId: impurityId);

        await svc.CreateSpecificationAsync(userId, Row(RelatedSubstanceRow.Total, 0.5m));
        await svc.CreateSpecificationAsync(userId, Row(RelatedSubstanceRow.UnspecifiedEach, 0.10m));
        await svc.CreateSpecificationAsync(userId, Row(RelatedSubstanceRow.Named, 0.15m, method.Impurities[0].Id));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateSpecificationAsync(userId, Row(RelatedSubstanceRow.Total, 0.6m)));
        Assert.Equal("A total impurities row already exists for this test, stage and grade.", ex.Message);
        var named = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateSpecificationAsync(userId, Row(RelatedSubstanceRow.Named, 0.2m, method.Impurities[0].Id)));
        Assert.Equal("A named impurity 'Impurity D' row already exists for this test, stage and grade.", named.Message);

        await using var check = _fixture.CreateDbContext();
        Assert.Equal(3, await check.Specifications.CountAsync(s => s.ItemId == item.Id));

        // The restrict FK keeps a row's impurity in place.
        var impurity = await check.HplcMethodImpurities.SingleAsync(i => i.Id == method.Impurities[0].Id);
        check.HplcMethodImpurities.Remove(impurity);
        await Assert.ThrowsAsync<DbUpdateException>(() => check.SaveChangesAsync());

        // The workbook round-trip test shares this database and does not carry related-substance rows yet.
        await using var cleanup = _fixture.CreateDbContext();
        await cleanup.Specifications.Where(s => s.ItemId == item.Id).ExecuteDeleteAsync();
    }
}
