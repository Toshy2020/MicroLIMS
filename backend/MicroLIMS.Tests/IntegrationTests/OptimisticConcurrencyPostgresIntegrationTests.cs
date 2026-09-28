using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Two people editing the same record used to be last-write-wins: the
// second save silently replaced the first. Every table now carries
// PostgreSQL's xmin as a concurrency token, so the second save fails
// and is answered with 409 instead.
[Collection("PostgresDatabaseCollection")]
public class OptimisticConcurrencyPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public OptimisticConcurrencyPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<int> NewItemAsync(string name)
    {
        await using var db = _fixture.CreateDbContext();
        var item = new Item { Code = "OCC-" + Guid.NewGuid().ToString("N")[..10], Name = name };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    [PostgresFact]
    public async Task SecondOfTwoConcurrentEdits_IsRejected_AndTheFirstIsKept()
    {
        var id = await NewItemAsync("original");
        await using var alice = _fixture.CreateDbContext();
        await using var bob = _fixture.CreateDbContext();
        var alicesCopy = await alice.Items.SingleAsync(i => i.Id == id);
        var bobsCopy = await bob.Items.SingleAsync(i => i.Id == id);

        alicesCopy.Name = "Alice's edit";
        await alice.SaveChangesAsync();

        bobsCopy.Name = "Bob's edit";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => bob.SaveChangesAsync());

        await using var check = _fixture.CreateDbContext();
        Assert.Equal("Alice's edit", (await check.Items.SingleAsync(i => i.Id == id)).Name);
    }

    [PostgresFact]
    public async Task DeletingARecordSomeoneElseJustChanged_IsRejected()
    {
        var id = await NewItemAsync("original");
        await using var editor = _fixture.CreateDbContext();
        await using var deleter = _fixture.CreateDbContext();
        var edited = await editor.Items.SingleAsync(i => i.Id == id);
        var deleted = await deleter.Items.SingleAsync(i => i.Id == id);

        edited.Name = "edited";
        await editor.SaveChangesAsync();

        deleter.Items.Remove(deleted);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => deleter.SaveChangesAsync());

        await using var check = _fixture.CreateDbContext();
        Assert.True(await check.Items.AnyAsync(i => i.Id == id));
    }

    // The token is refreshed from the database after every save, so one
    // context can keep editing the same record.
    [PostgresFact]
    public async Task OneContext_CanSaveTheSameRecordRepeatedly()
    {
        var id = await NewItemAsync("v0");
        await using var db = _fixture.CreateDbContext();
        var item = await db.Items.SingleAsync(i => i.Id == id);

        for (var v = 1; v <= 3; v++)
        {
            item.Name = $"v{v}";
            await db.SaveChangesAsync();
        }

        await using var check = _fixture.CreateDbContext();
        Assert.Equal("v3", (await check.Items.SingleAsync(i => i.Id == id)).Name);
    }

    // ---- How the API answers a conflict ----

    private sealed class NoOpErrorCapture : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task Conflict_IsAnswered409_WithAReloadMessage_NotA500()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IErrorCaptureService, NoOpErrorCapture>();
        await using var app = builder.Build();
        app.UseMiddleware<ExceptionMiddleware>();
        app.MapPost("/save", (HttpContext _) =>
        {
            throw new DbUpdateConcurrencyException("The database operation was expected to affect 1 row(s), but actually affected 0 row(s).");
#pragma warning disable CS0162
            return Results.Ok();
#pragma warning restore CS0162
        });
        await app.StartAsync();

        var response = await app.GetTestClient().PostAsync("/save", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("changed by someone else", body);
        Assert.DoesNotContain("affected 0 row", body);
    }
}
