using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MicroLIMS.API.Controllers;
using MicroLIMS.API.Filters;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// MustChangePassword was previously returned to the browser and honoured
// only by the frontend, so any client that ignored it could work normally
// with a password that was meant to be replaced first. These cover the
// server-side gate that makes the provisioning password of the first
// administrator genuinely single-use.
public class MustChangePasswordEnforcementTests
{
    private const int UserId = 42;

    private static MicroLimsDbContext CreateDbContext(bool mustChangePassword)
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        db.Roles.Add(new Role { Id = 1, Type = RoleType.SystemAdministrator, Name = "System Administrator" });
        db.Users.Add(new User
        {
            Id = UserId,
            FullName = "System Administrator",
            Username = "admin",
            PasswordHash = "hash",
            RoleId = 1,
            IsActive = true,
            MustChangePassword = mustChangePassword
        });
        db.SaveChanges();
        return db;
    }

    // Builds a filter context for a given controller action, optionally
    // authenticated as the seeded user.
    private static ActionExecutingContext Context(
        MicroLimsDbContext db, Type controllerType, string actionName, bool authenticated = true)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (authenticated)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()) }, "TestAuth"));
        }

        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            ActionName = actionName
        };

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), descriptor),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    private static async Task<(ActionExecutingContext Ctx, bool ReachedAction)> RunAsync(ActionExecutingContext ctx)
    {
        var reached = false;
        await new MustChangePasswordFilter().OnActionExecutionAsync(ctx, () =>
        {
            reached = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });
        return (ctx, reached);
    }

    // ---- 4. Ordinary operations are refused while the flag is set ----

    [Fact]
    public async Task FlaggedUser_IsRefusedOrdinaryOperations()
    {
        using var db = CreateDbContext(mustChangePassword: true);

        var (ctx, reached) = await RunAsync(Context(db, typeof(SampleController), "GetAll"));

        Assert.False(reached);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        var payload = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.False(payload.Success);
        Assert.Contains("password must be changed", payload.Message, StringComparison.OrdinalIgnoreCase);
    }

    // The three things a user in that state legitimately needs.
    [Theory]
    [InlineData("ChangePassword")]
    [InlineData("Me")]
    [InlineData("Logout")]
    public async Task FlaggedUser_MayStillReachTheRecoveryActions(string actionName)
    {
        using var db = CreateDbContext(mustChangePassword: true);

        var (_, reached) = await RunAsync(Context(db, typeof(AuthenticationController), actionName));

        Assert.True(reached);
    }

    // An action of the same name on another controller must not be let
    // through by the allow-list.
    [Fact]
    public async Task FlaggedUser_IsRefusedASimilarlyNamedActionOnAnotherController()
    {
        using var db = CreateDbContext(mustChangePassword: true);

        var (ctx, reached) = await RunAsync(Context(db, typeof(SampleController), "Me"));

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(ctx.Result).StatusCode);
    }

    // ---- 5 & 6. Normal operation once the flag is cleared ----

    [Fact]
    public async Task UnflaggedUser_PassesThroughNormally()
    {
        using var db = CreateDbContext(mustChangePassword: false);

        var (ctx, reached) = await RunAsync(Context(db, typeof(SampleController), "GetAll"));

        Assert.True(reached);
        Assert.Null(ctx.Result);
    }

    // Mirrors what ChangePasswordAsync does: clearing the flag restores
    // ordinary access without any further intervention.
    [Fact]
    public async Task AfterTheFlagIsCleared_TheSameUserRegainsAccess()
    {
        using var db = CreateDbContext(mustChangePassword: true);

        Assert.False((await RunAsync(Context(db, typeof(SampleController), "GetAll"))).ReachedAction);

        var user = await db.Users.FirstAsync(u => u.Id == UserId);
        user.MustChangePassword = false;
        await db.SaveChangesAsync();

        Assert.True((await RunAsync(Context(db, typeof(SampleController), "GetAll"))).ReachedAction);
    }

    // ---- Anonymous endpoints are not this filter's business ----

    [Fact]
    public async Task AnonymousRequest_IsNotBlocked()
    {
        using var db = CreateDbContext(mustChangePassword: true);

        var (_, reached) = await RunAsync(
            Context(db, typeof(AuthenticationController), "Login", authenticated: false));

        Assert.True(reached);
    }
}
