using System.Reflection;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Who can call each endpoint, per built-in role, on a default installation:
// every [Authorize] on the controller and the action must pass, and a
// permission policy admits the roles DbSeeder grants that permission to.
// authorization-matrix.txt is the reviewed expectation - a change to it is
// a change to who can do what, and shows up in the diff as such.
public class AuthorizationMatrixTests
{
    private const string SnapshotFile = "authorization-matrix.txt";

    private static string SnapshotPath([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, SnapshotFile);

    // The permission codes a built-in role holds on a default installation -
    // what JwtTokenService puts in that role's token.
    public static IReadOnlyList<string> DefaultPermissionsOf(RoleType role) =>
        Grants.Value.Where(g => g.Value.Contains(role)).Select(g => g.Key).OrderBy(c => c).ToList();

    private static Dictionary<string, HashSet<RoleType>> DefaultGrants()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var type in Enum.GetValues<RoleType>())
            db.Roles.Add(new Role { Type = type, Name = type.ToString(), IsSystemRole = true, IsActive = true });
        db.SaveChanges();
        DbSeeder.SeedPermissionsAndGrants(db);

        return db.RolePermissions
            .Select(rp => new { rp.Permission!.Code, rp.Role!.Type })
            .AsEnumerable()
            .GroupBy(x => x.Code)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Type).ToHashSet());
    }

    private static readonly Lazy<Dictionary<string, HashSet<RoleType>>> Grants = new(DefaultGrants);

    // The built-in roles that may call this action on a default installation;
    // null when it is anonymous.
    public static HashSet<RoleType>? AllowedRoles(Type controller, MethodInfo action)
    {
        if (action.GetCustomAttribute<AllowAnonymousAttribute>() is not null
            || controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null)
            return null;

        var allowed = Enum.GetValues<RoleType>().ToHashSet();
        var attributes = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>());
        foreach (var attribute in attributes)
        {
            if (!string.IsNullOrEmpty(attribute.Roles))
                allowed.IntersectWith(attribute.Roles.Split(',').Select(r => Enum.Parse<RoleType>(r.Trim())));
            if (!string.IsNullOrEmpty(attribute.Policy))
            {
                Assert.True(PermissionConstants.All.Contains(attribute.Policy), $"{controller.Name}.{action.Name}: unknown policy {attribute.Policy}");
                allowed.IntersectWith(Grants.Value.GetValueOrDefault(attribute.Policy) ?? new HashSet<RoleType>());
            }
        }
        return allowed;
    }

    public static HashSet<RoleType>? AllowedRoles<TController>(string actionName) =>
        AllowedRoles(typeof(TController), typeof(TController).GetMethod(actionName)!);

    public static List<string> Compute()
    {
        var lines = new List<string>();
        var controllers = typeof(MicroLIMS.API.Controllers.SampleController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllers)
        foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any()))
        {
            var signature = $"{controller.Name}.{action.Name}({string.Join(", ", action.GetParameters().Select(p => p.ParameterType.Name))})";
            var allowed = AllowedRoles(controller, action);
            lines.Add(allowed is null ? $"{signature}: anonymous" : $"{signature}: {string.Join(",", allowed.OrderBy(r => r))}");
        }

        return lines.OrderBy(l => l, StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void EffectiveAccess_MatchesTheReviewedMatrix()
    {
        var actual = Compute();
        if (Environment.GetEnvironmentVariable("MICROLIMS_WRITE_AUTH_MATRIX") == "1")
            File.WriteAllLines(SnapshotPath(), actual);

        var expected = File.ReadAllLines(SnapshotPath()).Where(l => l.Length > 0).ToList();
        var missing = expected.Except(actual).ToList();
        var added = actual.Except(expected).ToList();
        Assert.True(missing.Count == 0 && added.Count == 0,
            "Access changed.\nWas:\n  " + string.Join("\n  ", missing) + "\nNow:\n  " + string.Join("\n  ", added));
    }
}
