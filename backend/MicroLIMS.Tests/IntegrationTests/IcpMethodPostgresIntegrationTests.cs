using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class IcpMethodPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public IcpMethodPostgresIntegrationTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task CreateUpdateDeactivate_RoundTrip()
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var userId = _fixture.SeededUserId;

        await using var db = _fixture.CreateDbContext();
        var section = TestServiceFactory.EnsureMicroSection(db);
        TestServiceFactory.AssignUserToMicroSection(db, userId);

        var std = NewStandard(section.Id, "RS-A-" + tag);
        var icv = NewStandard(section.Id, "RS-B-" + tag);
        db.MaterialMasterEntries.AddRange(std, icv);
        await db.SaveChangesAsync();

        var svc = TestServiceFactory.IcpMethod(db);
        var request = new SaveIcpMethodRequest(
            Name: "Minerals " + tag, Abbreviation: "m" + tag, EffectiveDate: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            Mode: IcpMethodMode.MineralAssay, StandardLevelsMgPerL: "1, 0.1, 0.5",
            CalibrationStandardEntryId: std.Id, MinCorrelation: 0.999m, SampleVolumeMl: 50m,
            Elements: new() { new IcpElementInput(null, "zn", 213.857m, AnalyteView.Axial), new IcpElementInput(null, "ca", 317.933m, AnalyteView.Radial) },
            SectionId: section.Id, RequireIcv: true, IcvStandardEntryId: icv.Id, IcvNominalMgPerL: 1m, IcvRecoveryLowPercent: 90m, IcvRecoveryHighPercent: 110m);

        var created = await svc.CreateAsync(request, userId);
        var ids = created.Elements.OrderBy(e => e.DisplayOrder).Select(e => e.Id).ToList();

        await using (var check = _fixture.CreateDbContext())
        {
            var saved = await check.IcpMethods.Include(m => m.Elements).SingleAsync(m => m.Id == created.Id);
            Assert.Equal("M" + tag, saved.Abbreviation);
            Assert.Equal("0.1, 0.5, 1", saved.StandardLevelsMgPerL);
            Assert.True(saved.RequireIcv);
            Assert.Equal(icv.Id, saved.IcvStandardEntryId);
            Assert.Equal(new[] { "Zn", "Ca" }, saved.Elements.OrderBy(e => e.DisplayOrder).Select(e => e.Symbol));
        }

        // Update keeps both element ids and edits one wavelength.
        var updated = await svc.UpdateAsync(created.Id, request with
        {
            Name = "Minerals v2 " + tag, Reason = "Wavelength change",
            Elements = new()
            {
                new IcpElementInput(ids[0], "Zn", 206.200m, AnalyteView.Axial),
                new IcpElementInput(ids[1], "Ca", 317.933m, AnalyteView.Radial)
            }
        }, userId);
        Assert.Equal(ids, updated.Elements.OrderBy(e => e.DisplayOrder).Select(e => e.Id));

        // Link a test definition and one specification row to the first element.
        var def = await new TestDefinitionMasterDataService(db, new UserSectionScopeService(db))
            .CreateTestDefinitionAsync(userId, new CreateTestDefinitionRequest(
                Code: "ICP-" + tag, DisplayName: "ICP " + tag, SectionId: section.Id,
                WorkflowType: WorkflowType.IcpMethodAssay, EquationType: EquationType.IcpMethodAssay,
                RequiresSystemSuitability: false, MethodAbbreviation: "ICP-" + tag, IcpMethodId: created.Id));
        var item = new Item
        {
            Code = "ITEM-" + tag, Name = "Item " + tag,
            AssignedTests = { new SampleTest { TestCode = def.Code, DisplayName = def.Code } }
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        await new SpecificationMasterDataService(db, new UserSectionScopeService(db)).CreateSpecificationAsync(userId,
            new CreateSpecificationRequest(
                ItemId: item.Id, TestCode: def.Code, ParameterName: "Zn", LimitType: LimitType.Range,
                LowerLimit: 90m, UpperLimit: 110m, ResultBasis: ResultBasis.MgPerUnit,
                LabelClaim: 10m, LabelClaimUnit: "mg", IcpMethodElementId: ids[0]));

        var deactivated = await svc.SetActiveAsync(created.Id, false, "Superseded", userId);
        Assert.False(deactivated.IsActive);

        await using var final = _fixture.CreateDbContext();
        var method = await final.IcpMethods.Include(m => m.Elements).SingleAsync(m => m.Id == created.Id);
        Assert.False(method.IsActive);
        Assert.Equal("Minerals v2 " + tag, method.Name);
        Assert.Equal(ids, method.Elements.OrderBy(e => e.DisplayOrder).Select(e => e.Id));
        Assert.Equal(206.200m, method.Elements.Single(e => e.Id == ids[0]).WavelengthNm);
        Assert.Equal(created.Id, (await final.TestDefinitions.SingleAsync(t => t.Code == def.Code)).IcpMethodId);
        Assert.Equal(ids[0], (await final.Specifications.SingleAsync(s => s.ItemId == item.Id)).IcpMethodElementId);
        Assert.Equal(new[] { "IcpMethod.Created", "IcpMethod.Updated" },
            await final.AuditLogs.Where(a => a.EntityName == nameof(IcpMethod) && a.EntityId == created.Id.ToString() && a.ActionCode != null && a.ActionCode != "IcpMethod.Deactivated")
                .OrderBy(a => a.Id).Select(a => a.ActionCode!).ToListAsync());
    }

    private static MaterialMasterEntry NewStandard(int sectionId, string code) => new()
    {
        SectionId = sectionId, Code = code, Name = code, Category = MaterialMasterCategory.ReferenceStandard,
        BaseUnit = MaterialUnit.Gram, IsActive = true, CreatedByUserId = 1, CreatedAt = DateTime.UtcNow,
        LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow
    };
}
