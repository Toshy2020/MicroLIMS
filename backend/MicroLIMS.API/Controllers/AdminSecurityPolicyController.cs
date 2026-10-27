using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Read-only view of the effective security policy for the admin panel. The
// panel controls the system, so this only reports; it never changes a value.
// Policies cannot express "Users.Manage or Roles.Manage", so Users.Manage.
[ApiController]
[Route("api/admin/security-policy")]
[Authorize(Policy = PermissionConstants.UsersManage)]
public class AdminSecurityPolicyController : ControllerBase
{
    private readonly ISecurityPolicyService _service;

    public AdminSecurityPolicyController(ISecurityPolicyService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult Get() => Ok(ApiResponse<object>.Ok(_service.GetEffectivePolicy()));
}
