using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PrimaryStandardMaterialTests
{
    [Fact] public void Purity_IsRequired_ForPrimaryStandard() =>
        Assert.Throws<InvalidOperationException>(() => MaterialService.ValidatePurity(MaterialType.PrimaryStandard, null));
    [Fact] public void Purity_IsAccepted_ForPrimaryStandard() =>
        MaterialService.ValidatePurity(MaterialType.PrimaryStandard, 99.95m);
    [Fact] public void Purity_StillRejected_ForChemical() =>
        Assert.Throws<InvalidOperationException>(() => MaterialService.ValidatePurity(MaterialType.Chemical, 99m));
    [Fact] public void PrimaryStandard_NeedsMasterEntry() =>
        Assert.True(MaterialService.RequiresMasterEntry(MaterialType.PrimaryStandard));
    [Fact] public void Physicochemical_Offers_PrimaryStandard_NotMicroTypes()
    {
        var t = MaterialTypeRules.BuiltInTypesFor("FP");
        Assert.Contains(MaterialType.PrimaryStandard, t);
        Assert.DoesNotContain(MaterialType.DehydratedMedia, t);
        Assert.DoesNotContain(MaterialType.LyophilizedMicroorganism, t);
    }
    [Fact] public void Microbiology_DoesNotOffer_PrimaryStandard() =>
        Assert.DoesNotContain(MaterialType.PrimaryStandard, MaterialTypeRules.BuiltInTypesFor("MICRO"));

    [Fact]
    public async Task Create_PrimaryStandardLot_LinkedToReferenceStandardEntry_IsRefused()
    {
        await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        db.SaveChanges();
        var user = new User { Username = "fp_" + Guid.NewGuid().ToString("N")[..6], FullName = "Physico", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        db.SaveChanges();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = fp.DepartmentId, SectionId = fp.Id });
        var entry = new MaterialMasterEntry
        {
            SectionId = fp.Id, Code = "RS-1", Name = "Ref std", Category = MaterialMasterCategory.ReferenceStandard,
            BaseUnit = MaterialUnit.Gram, IsActive = true
        };
        db.MaterialMasterEntries.Add(entry);
        db.SaveChanges();
        var svc = new MaterialService(db, new UserSectionScopeService(db));

        var req = new SaveMaterialRequest(MaterialType.PrimaryStandard, "x", "Merck", "LOT-1",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, "Shelf", 10m, MaterialUnit.Gram, null, null, null,
            MaterialMasterEntryId: entry.Id, Purity: 99.9m);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(req, user.Id));
        Assert.Contains("is a ReferenceStandard, not a PrimaryStandard", ex.Message);
    }
}
