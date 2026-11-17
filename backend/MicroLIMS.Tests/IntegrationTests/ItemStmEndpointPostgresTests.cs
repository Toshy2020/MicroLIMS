using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// GET api/items/{id}/stm as the sample screens call it: an item that exists but
// has no STM is a normal answer (200, status NotLinked), not an error; only an
// item that does not exist is a 404.
[Collection("PostgresDatabaseCollection")]
public class ItemStmEndpointPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public ItemStmEndpointPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private ItemController Controller(MicroLIMS.Persistence.DbContext.MicroLimsDbContext db) =>
        new(null!, new ItemStmService(db), null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [PostgresFact]
    public async Task ItemWithoutSop_Is200_WithNotLinked()
    {
        int itemId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var item = new Item { Code = $"IT-{Guid.NewGuid():N}"[..16], Name = "No STM", SopNumber = "" };
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();
            itemId = item.Id;
        }

        IActionResult result;
        await using (var actDb = _fixture.CreateDbContext())
            result = await Controller(actDb).GetStm(itemId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<ItemStmResponse>>(ok.Value);
        Assert.Equal(ItemStmStatus.NotLinked, body.Data!.Status);
    }

    [PostgresFact]
    public async Task MissingItem_Is404()
    {
        IActionResult result;
        await using (var actDb = _fixture.CreateDbContext())
            result = await Controller(actDb).GetStm(int.MaxValue);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
