using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Middleware;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Pure parsing/dispatch of the X-Draft-Started-At header. The database is never
// touched: the middleware only sets a property on the request's DbContext.
public class DraftContextMiddlewareTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql("Host=unused").Options);

    private static async Task<(string? SourceContext, bool NextCalled)> RunAsync(string? header)
    {
        var http = new DefaultHttpContext();
        if (header is not null)
            http.Request.Headers[DraftContextMiddleware.HeaderName] = header;
        var nextCalled = false;
        var middleware = new DraftContextMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        await using var db = NewDb();

        await middleware.InvokeAsync(http, db);

        return (db.CurrentSourceContext, nextCalled);
    }

    [Theory]
    [InlineData("2026-11-13T09:15:42+02:00", "2026-11-13 07:15")]
    [InlineData("2026-11-13T23:59:59-05:00", "2026-11-14 04:59")]
    [InlineData("2026-11-13T09:15:42Z", "2026-11-13 09:15")]
    [InlineData("2041-03-04T05:06:00Z", "2041-03-04 05:06")]
    public async Task ValidHeader_SetsExactTextInUtc(string header, string utc)
    {
        var (context, nextCalled) = await RunAsync(header);

        Assert.Equal($"Submitted from a device draft started at {utc} UTC (device clock, not verified)", context);
        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("2026-11-13T09:15:42")]
    public async Task InvalidOrMissingHeader_SetsNothing_AndProceeds(string? header)
    {
        var (context, nextCalled) = await RunAsync(header);

        Assert.Null(context);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task HeaderOf65Chars_SetsNothing()
    {
        var padded = "2026-11-13T09:15:42." + new string('0', 44) + "Z";
        Assert.Equal(65, padded.Length);

        var (context, nextCalled) = await RunAsync(padded);

        Assert.Null(context);
        Assert.True(nextCalled);
    }

    [Fact]
    public void Constructor_TakesNoScopedDependency()
    {
        // Middleware is a singleton (CLAUDE.md 5.5): the DbContext must arrive in InvokeAsync.
        var ctor = Assert.Single(typeof(DraftContextMiddleware).GetConstructors());
        Assert.All(ctor.GetParameters(), p => Assert.Equal(typeof(RequestDelegate), p.ParameterType));
        var invoke = typeof(DraftContextMiddleware).GetMethod(nameof(DraftContextMiddleware.InvokeAsync))!;
        Assert.Contains(invoke.GetParameters(), p => p.ParameterType == typeof(MicroLimsDbContext));
    }
}
