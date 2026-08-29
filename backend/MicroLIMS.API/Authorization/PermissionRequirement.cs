using Microsoft.AspNetCore.Authorization;

namespace MicroLIMS.API.Authorization;

// One requirement per permission code - the policy name IS the
// permission code (see PermissionPolicyProvider), so this just carries
// that code through to the handler.
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}
