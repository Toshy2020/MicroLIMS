using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Json;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// POST /api/items used to bind the Item entity straight from the request
// body, so a caller could set Id, IsActive and a nested Specifications graph
// (the microbial limits) that item creation is not meant to touch.
public class ItemMassAssignmentTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Parsed exactly as the API parses a request body.
    private static ItemSaveRequest ParseAsTheApiDoes(string json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);
        return JsonSerializer.Deserialize<ItemSaveRequest>(json, options)!;
    }

    [Fact]
    public async Task Create_IgnoresIdIsActiveAndSpecifications_InTheRequestBody()
    {
        const string hostile = """
            {
              "id": 999,
              "isActive": false,
              "name": "Purified Water Tablets",
              "code": "PWT-01",
              "category": "FinishedProduct",
              "sopNumber": "SOP-QC-101",
              "assignedTests": [ { "testCode": "TAMC", "displayName": "Total Aerobic Microbial Count" } ],
              "specifications": [ { "testCode": "TAMC", "alertLimit": "100000", "actionLimit": "100000", "specLimit": "100000" } ]
            }
            """;

        await using var db = NewDb();
        var created = await new ItemService(db).CreateAsync(ParseAsTheApiDoes(hostile));

        var stored = await db.Items.AsNoTracking()
            .Include(i => i.AssignedTests).Include(i => i.Specifications)
            .SingleAsync();

        Assert.NotEqual(999, stored.Id);
        Assert.Equal(created.Id, stored.Id);
        Assert.True(stored.IsActive);
        Assert.Empty(stored.Specifications);
        Assert.Equal(0, await db.Specifications.CountAsync());

        // The fields a client may set are all applied.
        Assert.Equal("Purified Water Tablets", stored.Name);
        Assert.Equal("PWT-01", stored.Code);
        Assert.Equal(SampleCategory.FinishedProduct, stored.Category);
        Assert.Equal("SOP-QC-101", stored.SopNumber);
        var test = Assert.Single(stored.AssignedTests);
        Assert.Equal("TAMC", test.TestCode);
    }

    [Fact]
    public async Task Update_AppliesTheAllowedFields_AndLeavesSpecificationsAndActiveStateAlone()
    {
        await using var db = NewDb();
        var service = new ItemService(db);
        var item = await service.CreateAsync(new ItemSaveRequest("Tablets", "TAB-01", SampleCategory.FinishedProduct, "SOP-1",
            new List<ItemTestRequest> { new("TAMC", "TAMC") }));
        db.Specifications.Add(new Domain.Entities.Specification { ItemId = item.Id, TestCode = "TAMC", SpecLimit = "1000" });
        await db.SaveChangesAsync();
        await service.SetActiveAsync(item.Id, false);

        await service.UpdateAsync(item.Id, ParseAsTheApiDoes("""
            { "id": 999, "isActive": true, "name": "Tablets v2", "code": "TAB-02", "category": "RawMaterial",
              "sopNumber": null, "assignedTests": [ { "testCode": "TYMC", "displayName": "TYMC" } ], "specifications": [] }
            """));

        var stored = await db.Items.AsNoTracking()
            .Include(i => i.AssignedTests).Include(i => i.Specifications)
            .SingleAsync(i => i.Id == item.Id);
        Assert.Equal("Tablets v2", stored.Name);
        Assert.Equal("TAB-02", stored.Code);
        Assert.Equal(SampleCategory.RawMaterial, stored.Category);
        Assert.Equal(string.Empty, stored.SopNumber);
        Assert.Equal("TYMC", Assert.Single(stored.AssignedTests).TestCode);
        Assert.False(stored.IsActive);
        Assert.Single(stored.Specifications);
    }

    [Fact]
    public async Task Create_StillRefusesCategoriesManagedElsewhere()
    {
        await using var db = NewDb();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ItemService(db).CreateAsync(
            new ItemSaveRequest("Water point", "W-1", SampleCategory.Water, null, null)));
    }

    // API-wide guard: no controller action may take a domain entity as its
    // request model, so the same mass-assignment hole cannot come back
    // through another controller.
    [Fact]
    public void NoControllerAction_BindsADomainEntity()
    {
        var entityNamespace = typeof(Domain.Entities.Item).Namespace;
        var controllers = typeof(API.Controllers.ItemController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        static Type Element(Type t) =>
            t.IsGenericType && t.GetGenericArguments().Length == 1 ? t.GetGenericArguments()[0] : t;

        var offenders = controllers
            .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(m => m.GetParameters().Select(p => (Controller: c, Method: m, Parameter: p))))
            .Where(x => Element(x.Parameter.ParameterType).Namespace == entityNamespace)
            .Select(x => $"{x.Controller.Name}.{x.Method.Name}({x.Parameter.ParameterType.Name} {x.Parameter.Name})")
            .ToList();

        Assert.Empty(offenders);
    }
}
