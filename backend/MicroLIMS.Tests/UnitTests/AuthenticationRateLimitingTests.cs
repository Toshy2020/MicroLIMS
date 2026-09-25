using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Extensions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Login, password reset and admin recovery were unlimited, so the only
// brake on guessing across accounts was the per-account lockout. These
// pin the limits, their wiring on the real controller, and which client
// address the limits (and the audit trail) believe.
public class AuthenticationRateLimitingTests
{
    // ---- Wiring on the real controller ----

    [Theory]
    [InlineData(nameof(AuthenticationController.Login), RateLimitingPolicies.Authentication)]
    [InlineData(nameof(AuthenticationController.RequestPasswordReset), RateLimitingPolicies.Authentication)]
    [InlineData(nameof(AuthenticationController.ConfirmPasswordReset), RateLimitingPolicies.Authentication)]
    [InlineData(nameof(AuthenticationController.ConfirmAdminPasswordRecovery), RateLimitingPolicies.Authentication)]
    [InlineData(nameof(AuthenticationController.Refresh), RateLimitingPolicies.TokenRefresh)]
    [InlineData(nameof(AuthenticationController.ChangePassword), RateLimitingPolicies.PasswordChange)]
    public void AuthenticationEndpoint_CarriesItsRateLimitPolicy(string action, string policy)
    {
        var method = typeof(AuthenticationController).GetMethod(action)!;
        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(policy, attribute!.PolicyName);
    }

    // Every anonymous POST that checks a secret must be limited, so a new
    // one cannot be added without a policy.
    [Fact]
    public void EveryAnonymousAuthenticationPost_IsRateLimited()
    {
        var unlimited = typeof(AuthenticationController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a => a.HttpMethods.Contains("POST")))
            .Where(m => m.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>() is not null)
            .Where(m => m.GetCustomAttribute<EnableRateLimitingAttribute>() is null)
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(unlimited);
    }

    // ---- Behaviour, through the real policies and forwarded-header setup ----

    private const string SocketIpHeader = "X-Test-Socket-Ip";

    // Hosts the real AddMicroLimsRateLimiting and AddMicroLimsForwardedHeaders.
    // The test server has no socket address, so a first middleware sets one
    // from a test header to stand in for the connecting peer (the proxy).
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(
        params (string Key, string Value)[] settings)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)));
        builder.Services.AddMicroLimsForwardedHeaders(builder.Configuration);
        builder.Services.AddMicroLimsRateLimiting(builder.Configuration);

        var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(
                context.Request.Headers[SocketIpHeader].FirstOrDefault() ?? "10.0.0.1");
            if (context.Request.Headers["X-Test-User"].FirstOrDefault() is { } user)
                context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user) }, "test"));
            return next(context);
        });
        app.UseForwardedHeaders();
        app.UseRateLimiter();
        app.MapPost("/login", (HttpContext c) => c.Connection.RemoteIpAddress?.ToString())
            .RequireRateLimiting(RateLimitingPolicies.Authentication);
        app.MapPost("/change-password", () => "ok")
            .RequireRateLimiting(RateLimitingPolicies.PasswordChange);

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string? socketIp = null,
        string? forwardedFor = null, string? user = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (socketIp is not null) request.Headers.Add(SocketIpHeader, socketIp);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        if (user is not null) request.Headers.Add("X-Test-User", user);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Login_IsRefusedWith429_OnceTheClientExceedsTheLimit()
    {
        var (app, client) = await StartAsync(("RateLimiting:Authentication:PermitLimit", "3"));
        await using var _ = app;

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await Post(client, "/login", forwardedFor: "203.0.113.5")).StatusCode);

        var refused = await Post(client, "/login", forwardedFor: "203.0.113.5");
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.True(refused.Headers.Contains("Retry-After"));
        Assert.Contains("Too many requests", await refused.Content.ReadAsStringAsync());

        // Another client address has its own allowance.
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/login", forwardedFor: "203.0.113.6")).StatusCode);
    }

    [Fact]
    public async Task Login_DefaultLimit_Is20PerMinutePerClient()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        for (var i = 0; i < RateLimitingExtensions.DefaultAuthenticationPerWindow; i++)
            Assert.Equal(HttpStatusCode.OK, (await Post(client, "/login", forwardedFor: "203.0.113.7")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(client, "/login", forwardedFor: "203.0.113.7")).StatusCode);
    }

    // A proxy appends the address it saw; only that last entry is believed,
    // so an address the client prepends cannot dodge its own limit.
    [Fact]
    public async Task PrependedForwardedAddress_IsIgnored_SoItCannotEscapeTheLimit()
    {
        var (app, client) = await StartAsync(("RateLimiting:Authentication:PermitLimit", "2"));
        await using var _ = app;

        var first = await Post(client, "/login", forwardedFor: "198.51.100.1, 203.0.113.9");
        Assert.Equal("203.0.113.9", await first.Content.ReadAsStringAsync());
        await Post(client, "/login", forwardedFor: "198.51.100.2, 203.0.113.9");

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Post(client, "/login", forwardedFor: "198.51.100.3, 203.0.113.9")).StatusCode);
    }

    [Fact]
    public async Task WithTrustedProxiesConfigured_ForwardedHeadersFromAnywhereElse_AreIgnored()
    {
        var (app, client) = await StartAsync(("ForwardedHeaders:KnownNetworks", "10.20.0.0/16"));
        await using var _ = app;

        // Through the trusted proxy: the forwarded client address is used.
        var viaProxy = await Post(client, "/login", socketIp: "10.20.3.4", forwardedFor: "203.0.113.20");
        Assert.Equal("203.0.113.20", await viaProxy.Content.ReadAsStringAsync());

        // Straight to the API: the caller's own X-Forwarded-For is not believed.
        var direct = await Post(client, "/login", socketIp: "192.0.2.50", forwardedFor: "203.0.113.21");
        Assert.Equal("192.0.2.50", await direct.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PasswordChange_IsLimitedPerUser_NotPerAddress()
    {
        var (app, client) = await StartAsync(("RateLimiting:PasswordChange:PermitLimit", "2"));
        await using var _ = app;

        await Post(client, "/change-password", forwardedFor: "203.0.113.30", user: "7");
        await Post(client, "/change-password", forwardedFor: "203.0.113.31", user: "7");

        // Changing address does not reset the same user's allowance...
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await Post(client, "/change-password", forwardedFor: "203.0.113.32", user: "7")).StatusCode);
        // ...and another user on the first address is unaffected.
        Assert.Equal(HttpStatusCode.OK,
            (await Post(client, "/change-password", forwardedFor: "203.0.113.30", user: "8")).StatusCode);
    }
}
