using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using MicroLIMS.API.Middleware;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Deny-by-default authorization and security response headers - defaults
// that hold for every endpoint, including ones not written yet.
public class ApiHardeningDefaultsTests
{
    // Authenticates a request only when it carries X-Test-User.
    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Test-User"].FirstOrDefault() is not { } user)
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user) }, "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private static async Task<(WebApplication App, HttpClient Client)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddMicroLimsAuthorization();

        var app = builder.Build();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();

        // Deliberately no RequireAuthorization / AllowAnonymous: the fallback decides.
        app.MapGet("/unannotated", () => "ok");
        app.MapGet("/anonymous", () => "ok").AllowAnonymous();
        app.MapGet("/boom", (HttpContext _) => Results.Problem("boom")).AllowAnonymous();

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<HttpResponseMessage> Get(HttpClient client, string path, string? user = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (user is not null) request.Headers.Add("X-Test-User", user);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task EndpointWithoutAnyAuthorizeAttribute_RequiresASignedInUser()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get(client, "/unannotated")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/unannotated", user: "7")).StatusCode);
    }

    [Fact]
    public async Task ExplicitAllowAnonymous_StillWorksWithoutSigningIn()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/anonymous")).StatusCode);
    }

    [Theory]
    [InlineData("/anonymous", null)]     // ordinary 200
    [InlineData("/unannotated", null)]   // 401 challenge
    [InlineData("/boom", null)]          // 500 problem
    [InlineData("/unannotated", "7")]    // authenticated 200
    public async Task EveryResponse_CarriesTheSecurityHeaders(string path, string? user)
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var response = await Get(client, path, user);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
    }

    // The do-nothing PUT /api/testorders/{id} ("use /api/results ...")
    // pointed at an endpoint that no longer accepts writes.
    [Fact]
    public void TestingWorkspaceController_HasNoWriteRoutes()
    {
        var writes = typeof(TestingWorkspaceController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Where(verb => verb != "GET")
            .ToList();

        Assert.Empty(writes);
    }
}
