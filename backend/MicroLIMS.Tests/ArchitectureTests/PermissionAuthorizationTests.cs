using System.Reflection;
using System.Text.RegularExpressions;
using MicroLIMS.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Endpoints authorize by permission, so what the Roles screen grants is what
// a user can do. A role name is used only where a rule must not be
// grantable (System Administrators are granted every permission).
public class PermissionAuthorizationTests
{
    // Changing a media product code needs a Section Head's electronic
    // signature; administrators are deliberately excluded.
    private static readonly HashSet<string> RoleGatedByDesign = new() { "MediaMasterDataController.ChangeMediaProductCode" };

    private static IEnumerable<(string Name, AuthorizeAttribute Attribute)> AuthorizeAttributes() =>
        typeof(MicroLIMS.API.Controllers.SampleController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => (t.Name, a))
                .Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => ($"{t.Name}.{m.Name}", a)))));

    [Fact]
    public void NoEndpoint_AuthorizesByRoleName()
    {
        var offenders = AuthorizeAttributes()
            .Where(x => !string.IsNullOrEmpty(x.Attribute.Roles) && !RoleGatedByDesign.Contains(x.Name))
            .Select(x => $"{x.Name}: Roles = {x.Attribute.Roles}")
            .ToList();

        Assert.True(offenders.Count == 0, "Use [Authorize(Policy = PermissionConstants.X)]:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryPolicyInUse_IsMarkedEnforced()
    {
        var used = AuthorizeAttributes().Select(x => x.Attribute.Policy).OfType<string>().ToHashSet();
        Assert.Empty(used.Except(PermissionConstants.Enforced));
    }

    [Fact]
    public void EveryEnforcedCode_IsCheckedSomewhere()
    {
        var backend = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
        var sep = Path.DirectorySeparatorChar;
        var source = string.Join("\n", new[] { "MicroLIMS.API", "MicroLIMS.Application" }
            .SelectMany(p => Directory.EnumerateFiles(Path.Combine(backend, p), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
            .Select(File.ReadAllText));
        var referenced = typeof(PermissionConstants).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && Regex.IsMatch(source, $@"PermissionConstants\.{f.Name}\b"))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        Assert.Empty(PermissionConstants.Enforced.Except(referenced));
    }

    [Fact]
    public void ControllersDoNotCheckRoleNamesInCode()
    {
        var backend = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile())!, "..", ".."));
        var offenders = Directory.EnumerateFiles(Path.Combine(backend, "MicroLIMS.API", "Controllers"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(f => File.ReadLines(f).Select((l, i) => (File: f, Line: i + 1, Text: l)))
            .Where(l => l.Text.Contains("IsInRole("))
            .Select(l => $"{Path.GetFileName(l.File)}:{l.Line}")
            .ToList();

        Assert.True(offenders.Count == 0, "Check a permission claim instead:\n" + string.Join("\n", offenders));
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}
