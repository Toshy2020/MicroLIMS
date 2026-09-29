using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// A form left open while a colleague saved the same record used to
// overwrite the colleague's change: the server reloads the row on each
// request, so the xmin token only catches two saves racing each other.
// Records edited through forms now carry their Version out to the client
// and back as If-Match, and an edit from an out-of-date form is refused.
[Collection("PostgresDatabaseCollection")]
public class StaleFormPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public StaleFormPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static UpdateOrganismRequest Rename(string name) => new(name, null, null, null);

    private async Task<(int Id, uint Version)> NewOrganismAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var created = await new OrganismMasterDataService(db)
            .CreateOrganismAsync(new CreateOrganismRequest("Staphylococcus " + Guid.NewGuid().ToString("N")[..8], null, null, null));
        return (created.Id, created.Version);
    }

    [PostgresFact]
    public async Task EditFromAFormLoadedBeforeAColleaguesSave_IsRefused_AndTheColleaguesChangeIsKept()
    {
        var (id, loadedVersion) = await NewOrganismAsync();

        await using (var colleague = _fixture.CreateDbContext())
            await new OrganismMasterDataService(colleague).UpdateOrganismAsync(id, Rename("Colleague's name " + id));

        await using (var db = _fixture.CreateDbContext())
        {
            db.ExpectedVersion = loadedVersion;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                new OrganismMasterDataService(db).UpdateOrganismAsync(id, Rename("Stale form's name " + id)));
        }

        await using var check = _fixture.CreateDbContext();
        Assert.Equal("Colleague's name " + id, (await check.Organisms.SingleAsync(o => o.Id == id)).ScientificName);
    }

    [PostgresFact]
    public async Task EditFromACurrentForm_Saves_AndReturnsTheNewVersion()
    {
        var (id, loadedVersion) = await NewOrganismAsync();

        await using var db = _fixture.CreateDbContext();
        db.ExpectedVersion = loadedVersion;
        var saved = await new OrganismMasterDataService(db).UpdateOrganismAsync(id, Rename("Renamed " + id));

        Assert.Equal("Renamed " + id, saved.ScientificName);
        Assert.NotEqual(loadedVersion, saved.Version);

        // The returned version is the one the next edit must send.
        await using var next = _fixture.CreateDbContext();
        next.ExpectedVersion = saved.Version;
        await new OrganismMasterDataService(next).UpdateOrganismAsync(id, Rename("Renamed again " + id));
    }

    // A client that sends no If-Match (an older frontend, a script) keeps
    // today's behaviour; only concurrent saves are caught.
    [PostgresFact]
    public async Task EditWithoutAVersion_StillSaves()
    {
        var (id, _) = await NewOrganismAsync();

        await using (var colleague = _fixture.CreateDbContext())
            await new OrganismMasterDataService(colleague).UpdateOrganismAsync(id, Rename("Colleague " + id));

        await using var db = _fixture.CreateDbContext();
        var saved = await new OrganismMasterDataService(db).UpdateOrganismAsync(id, Rename("Unversioned " + id));
        Assert.Equal("Unversioned " + id, saved.ScientificName);
    }

    // Same rule on a record loaded with FindAsync (the item form).
    [PostgresFact]
    public async Task ItemEdit_FromAStaleForm_IsRefused()
    {
        int id;
        uint loaded;
        await using (var db = _fixture.CreateDbContext())
        {
            var created = await new ItemService(db).CreateAsync(new ItemSaveRequest("Tablet", "SF-" + Guid.NewGuid().ToString("N")[..8], SampleCategory.FinishedProduct, null, null));
            (id, loaded) = (created.Id, created.Version);
        }

        await using (var colleague = _fixture.CreateDbContext())
        {
            var item = await colleague.Items.SingleAsync(i => i.Id == id);
            await new ItemService(colleague).UpdateAsync(id, new ItemSaveRequest("Tablet (colleague)", item.Code, SampleCategory.FinishedProduct, null, null));
        }

        await using var stale = _fixture.CreateDbContext();
        var code = (await stale.Items.AsNoTracking().SingleAsync(i => i.Id == id)).Code;
        stale.ExpectedVersion = loaded;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            new ItemService(stale).UpdateAsync(id, new ItemSaveRequest("Tablet (stale)", code, SampleCategory.FinishedProduct, null, null)));
    }

    // ---- The If-Match header ----

    [Theory]
    [InlineData("\"123\"", 123u)]
    [InlineData("123", 123u)]
    [InlineData(null, null)]
    [InlineData("*", null)]
    [InlineData("W/\"123\"", null)]
    [InlineData("-5", null)]
    public async Task IfMatch_IsReadOntoTheDbContext(string? header, uint? expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<MicroLimsDbContext>(o => o.UseInMemoryDatabase("if-match-" + Guid.NewGuid()));
        await using var app = builder.Build();
        app.UseMiddleware<RecordVersionMiddleware>();
        app.MapGet("/", (MicroLimsDbContext db) => Results.Text(db.ExpectedVersion?.ToString() ?? "none"));
        await app.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (header is not null) request.Headers.TryAddWithoutValidation("If-Match", header);
        var body = await (await app.GetTestClient().SendAsync(request)).Content.ReadAsStringAsync();

        Assert.Equal(expected?.ToString() ?? "none", body);
    }
}
