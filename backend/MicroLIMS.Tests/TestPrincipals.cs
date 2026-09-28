using System.Security.Claims;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.ArchitectureTests;

namespace MicroLIMS.Tests;

// A signed-in user as the API sees one: the role claim plus one
// "permission" claim per code the role holds by default - the same claims
// JwtTokenService.IssueToken writes.
public static class TestPrincipals
{
    public static IEnumerable<Claim> RoleAndPermissionClaims(string roleName)
    {
        yield return new Claim(ClaimTypes.Role, roleName);
        if (Enum.TryParse<RoleType>(roleName, out var role))
            foreach (var code in AuthorizationMatrixTests.DefaultPermissionsOf(role))
                yield return new Claim("permission", code);
    }
}
