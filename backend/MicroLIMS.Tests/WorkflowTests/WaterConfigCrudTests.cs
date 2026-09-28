using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterConfigCrudTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public async Task SamplingPoint_CanBeLinkedToWaterDepartment()
    {
        await using var db = NewDb();
        var dept = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();

        db.WaterSamplingPoints.Add(new WaterSamplingPoint
        {
            Code = "SP106", Location = "WTU", TestingFrequency = "Weekly", WaterDepartmentId = dept.Id, AssignedTestCodes = new() { "TAMC-Water" }
        });
        await db.SaveChangesAsync();

        var loaded = await db.WaterDepartments.Include(d => d.SamplingPoints).FirstAsync(d => d.Id == dept.Id);
        Assert.Single(loaded.SamplingPoints);
        Assert.Equal("SP106", loaded.SamplingPoints[0].Code);
    }

    [Fact]
    public async Task CreateWaterDepartment_PersistsRow()
    {
        await using var db = NewDb();
        var controller = new MasterDataControllers(db, new MicroLIMS.Application.Services.EquipmentConfigurationService(db), TestServiceFactory.MediaProduct(db), TestServiceFactory.MediaIncubationCondition(db), new MicroLIMS.Application.Services.UserSectionScopeService(db), new MicroLIMS.Application.Services.ChromatographyColumnService(db, new MicroLIMS.Application.Services.UserSectionScopeService(db)));

        await controller.Water.CreateWaterDepartment(
            new MicroLIMS.Application.DTOs.CreateWaterDepartmentRequest("WTU"));

        var dept = await db.WaterDepartments.SingleAsync();
        Assert.Equal("WTU", dept.Name);
    }

    [Fact]
    public async Task DeleteWaterDepartment_WithSamplingPoints_Throws()
    {
        await using var db = NewDb();
        var dept = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();
        db.WaterSamplingPoints.Add(new WaterSamplingPoint { Code = "SP1", WaterDepartmentId = dept.Id });
        await db.SaveChangesAsync();

        var controller = new MasterDataControllers(db, new MicroLIMS.Application.Services.EquipmentConfigurationService(db), TestServiceFactory.MediaProduct(db), TestServiceFactory.MediaIncubationCondition(db), new MicroLIMS.Application.Services.UserSectionScopeService(db), new MicroLIMS.Application.Services.ChromatographyColumnService(db, new MicroLIMS.Application.Services.UserSectionScopeService(db)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Water.DeleteWaterDepartment(dept.Id));
    }

    [Fact]
    public async Task CreateSamplingPoint_StoresDepartmentId()
    {
        await using var db = NewDb();
        var dept = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();

        var controller = new MasterDataControllers(db, new MicroLIMS.Application.Services.EquipmentConfigurationService(db), TestServiceFactory.MediaProduct(db), TestServiceFactory.MediaIncubationCondition(db), new MicroLIMS.Application.Services.UserSectionScopeService(db), new MicroLIMS.Application.Services.ChromatographyColumnService(db, new MicroLIMS.Application.Services.UserSectionScopeService(db)));
        await controller.Water.CreateWaterSamplingPoint(new MicroLIMS.Application.DTOs.CreateWaterSamplingPointRequest(
            "SP205", "WTU", "Weekly", new List<string> { "TAMC-Water" }, dept.Id));

        var point = await db.WaterSamplingPoints.SingleAsync();
        Assert.Equal(dept.Id, point.WaterDepartmentId);
        Assert.Equal("Weekly", point.TestingFrequency);
    }

    [Fact]
    public async Task CreateWaterSamplingConfig_PersistsLimits()
    {
        await using var db = NewDb();
        var point = new WaterSamplingPoint { Code = "SP104", Location = "WTU" };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var controller = new MasterDataControllers(db, new MicroLIMS.Application.Services.EquipmentConfigurationService(db), TestServiceFactory.MediaProduct(db), TestServiceFactory.MediaIncubationCondition(db), new MicroLIMS.Application.Services.UserSectionScopeService(db), new MicroLIMS.Application.Services.ChromatographyColumnService(db, new MicroLIMS.Application.Services.UserSectionScopeService(db)));
        await controller.Water.CreateWaterSamplingConfiguration(new MicroLIMS.Application.DTOs.CreateWaterSamplingConfigRequest(
            point.Id, "TAMC-Water", "10", "50", "100"));

        var config = await db.SamplingConfigurations.SingleAsync();
        Assert.Equal(point.Id, config.WaterSamplingPointId);
        Assert.Equal("TAMC-Water", config.TestCode);
        Assert.Equal("50", config.ActionLimit);
    }
}
