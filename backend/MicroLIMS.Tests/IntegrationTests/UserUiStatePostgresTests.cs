using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Per-user interface preferences. UserUiState is excluded from audit capture
// (approved 2026-11-11), so the round trip also proves no AuditLogs row is made.
[Collection("PostgresDatabaseCollection")]
public class UserUiStatePostgresTests
{
    private static readonly DateTimeOffset FixedNow = new(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private readonly PostgresTestFixture _fixture;

    public UserUiStatePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private async Task<int> CreateUserAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        await using var db = _fixture.CreateDbContext();
        var roleId = await db.Users.AsNoTracking().Where(u => u.Id == _fixture.SeededUserId).Select(u => u.RoleId).SingleAsync();
        var user = new User
        {
            FullName = "Ui State " + tag,
            Username = "uistate_" + tag,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("UiState!Pass1", 4),
            RoleId = roleId,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private UserUiStateService NewService(MicroLimsDbContext db, TimeProvider? clock = null) =>
        new(db, clock ?? new FixedTimeProvider(FixedNow));

    [PostgresFact]
    public async Task Set_Get_Remove_RoundTrip()
    {
        var userId = await CreateUserAsync();

        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb).SetAsync(userId, "tour.analyst.v1", "done");

        await using (var assertDb = _fixture.CreateDbContext())
        {
            var row = await assertDb.UserUiStates.AsNoTracking().SingleAsync(s => s.UserId == userId);
            Assert.Equal("tour.analyst.v1", row.Key);
            Assert.Equal("done", row.Value);
            Assert.Equal(FixedNow.UtcDateTime, row.UpdatedAt);
            Assert.Equal(DateTimeKind.Utc, row.UpdatedAt.Kind);
        }

        // Overwrite changes the value and the time, and keeps one row.
        var later = new FixedTimeProvider(FixedNow.AddHours(2));
        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb, later).SetAsync(userId, "tour.analyst.v1", "skipped");

        await using (var getDb = _fixture.CreateDbContext())
        {
            var all = await NewService(getDb).GetAllAsync(userId);
            Assert.Equal(new Dictionary<string, string> { ["tour.analyst.v1"] = "skipped" }, all);
        }
        await using (var assertDb = _fixture.CreateDbContext())
        {
            var row = await assertDb.UserUiStates.AsNoTracking().SingleAsync(s => s.UserId == userId);
            Assert.Equal(FixedNow.AddHours(2).UtcDateTime, row.UpdatedAt);
        }

        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb).RemoveAsync(userId, "tour.analyst.v1");

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Empty(await assertDb.UserUiStates.AsNoTracking().Where(s => s.UserId == userId).ToListAsync());
            // Excluded from automatic audit capture: no row for any of the three writes.
            Assert.Empty(await assertDb.AuditLogs.AsNoTracking().Where(a => a.EntityName == nameof(UserUiState)).ToListAsync());
        }

        // Removing a key that is not there is harmless.
        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb).RemoveAsync(userId, "tour.analyst.v1");
    }

    [PostgresFact]
    public async Task UserCannotSeeAnotherUsersKeys()
    {
        var userA = await CreateUserAsync();
        var userB = await CreateUserAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb);
            await service.SetAsync(userA, "favourite.sample", "A-value");
            await service.SetAsync(userB, "favourite.sample", "B-value");
            await service.SetAsync(userB, "only.b", "x");
        }

        await using (var getDb = _fixture.CreateDbContext())
        {
            var service = NewService(getDb);
            Assert.Equal(new Dictionary<string, string> { ["favourite.sample"] = "A-value" }, await service.GetAllAsync(userA));
            Assert.Equal(2, (await service.GetAllAsync(userB)).Count);
        }

        // Removing A's key does not touch B's row with the same key.
        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb).RemoveAsync(userA, "favourite.sample");

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Empty(await assertDb.UserUiStates.AsNoTracking().Where(s => s.UserId == userA).ToListAsync());
            Assert.Equal("B-value", (await assertDb.UserUiStates.AsNoTracking().SingleAsync(s => s.UserId == userB && s.Key == "favourite.sample")).Value);
        }
    }

    [PostgresFact]
    public async Task InvalidKeyPattern_Rejected()
    {
        var userId = await CreateUserAsync();
        var invalid = new[]
        {
            "",
            "a",             // too short
            "Tour.done",     // starts upper case
            "1tour",         // starts with a digit
            "tour-done",     // hyphen
            "tour done",     // space
            "tour_done",     // underscore
        };

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb);
            foreach (var key in invalid)
                await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetAsync(userId, key, "v"));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.RemoveAsync(userId, "Bad Key"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Empty(await assertDb.UserUiStates.AsNoTracking().Where(s => s.UserId == userId).ToListAsync());
    }

    [PostgresFact]
    public async Task KeyOver64Characters_Rejected_And64Accepted()
    {
        var userId = await CreateUserAsync();
        var ok = "a" + new string('b', 63);
        var tooLong = ok + "c";

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb);
            await service.SetAsync(userId, ok, "v");
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetAsync(userId, tooLong, "v"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(new[] { ok }, await assertDb.UserUiStates.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Key).ToArrayAsync());
    }

    [PostgresFact]
    public async Task ValueOver2Kb_Rejected()
    {
        var userId = await CreateUserAsync();

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb);
            await service.SetAsync(userId, "exact.limit", new string('x', 2048));
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetAsync(userId, "too.big", new string('x', 2049)));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var keys = await assertDb.UserUiStates.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Key).ToListAsync();
        Assert.Equal(new[] { "exact.limit" }, keys);
    }

    [PostgresFact]
    public async Task Key101_Rejected()
    {
        var userId = await CreateUserAsync();

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            for (var i = 0; i < 100; i++)
                arrangeDb.UserUiStates.Add(new UserUiState { UserId = userId, Key = $"key.n{i}", Value = "v", UpdatedAt = FixedNow.UtcDateTime });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = NewService(actDb);
            // The 100th key exists; overwriting it is still allowed.
            await service.SetAsync(userId, "key.n99", "changed");
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.SetAsync(userId, "key.n100", "v"));
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(100, await assertDb.UserUiStates.AsNoTracking().CountAsync(s => s.UserId == userId));
        Assert.Equal("changed", (await assertDb.UserUiStates.AsNoTracking().SingleAsync(s => s.UserId == userId && s.Key == "key.n99")).Value);
        Assert.False(await assertDb.UserUiStates.AsNoTracking().AnyAsync(s => s.UserId == userId && s.Key == "key.n100"));
    }

    [PostgresFact]
    public async Task The100thNewKey_IsAccepted()
    {
        var userId = await CreateUserAsync();

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            for (var i = 0; i < 99; i++)
                arrangeDb.UserUiStates.Add(new UserUiState { UserId = userId, Key = $"key.n{i}", Value = "v", UpdatedAt = FixedNow.UtcDateTime });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
            await NewService(actDb).SetAsync(userId, "key.last", "v");

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Equal(100, await assertDb.UserUiStates.AsNoTracking().CountAsync(s => s.UserId == userId));
    }

    [PostgresFact]
    public async Task Controller_ActsOnTheCallersIdFromClaims()
    {
        var caller = await CreateUserAsync();
        var other = await CreateUserAsync();

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            arrangeDb.UserUiStates.Add(new UserUiState { UserId = other, Key = "shared.key", Value = "other", UpdatedAt = FixedNow.UtcDateTime });
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var controller = new UserUiStateController(NewService(actDb)) { ControllerContext = ContextFor(caller) };
            Assert.IsType<OkObjectResult>(await controller.Set("shared.key", new SetUiStateRequest("mine"), default));

            var get = Assert.IsType<OkObjectResult>(await controller.GetAll(default));
            var body = Assert.IsType<ApiResponse<IReadOnlyDictionary<string, string>>>(get.Value);
            Assert.Equal(new Dictionary<string, string> { ["shared.key"] = "mine" }, body.Data);

            Assert.IsType<OkObjectResult>(await controller.Remove("shared.key", default));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var rows = await assertDb.UserUiStates.AsNoTracking().Where(s => s.Key == "shared.key" && (s.UserId == caller || s.UserId == other)).ToListAsync();
        var remaining = Assert.Single(rows);
        Assert.Equal(other, remaining.UserId);
        Assert.Equal("other", remaining.Value);
    }

    [PostgresFact]
    public Task Unauthenticated_401() =>
        PostgresCases.RunAsync(new[] { typeof(UserUiStateController), typeof(SystemVersionController) }, controller =>
        {
            AssertRequiresSignIn(controller);
            return Task.CompletedTask;
        });

    private static void AssertRequiresSignIn(Type controller)
    {
        // The pipeline answers 401 when a controller carries [Authorize] and no
        // action opts out with [AllowAnonymous]; the authorization matrix pins the roles.
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [PostgresFact]
    public void Version_ReturnsAssemblyInformationalVersion()
    {
        var controller = new SystemVersionController(new StubEnvironment("Staging"));

        var result = Assert.IsType<OkObjectResult>(controller.Get());
        var body = Assert.IsType<ApiResponse<SystemVersionResponse>>(result.Value);

        var expected = typeof(SystemVersionController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.Equal(expected, body.Data!.Version);
        Assert.Equal("Staging", body.Data.Environment);
        Assert.Matches(@"^\d+\.\d+\.\d+", body.Data.Version);
    }

    private static ControllerContext ContextFor(int userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"))
        }
    };

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "MicroLIMS.API";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
