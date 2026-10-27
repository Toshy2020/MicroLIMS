using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Read-only admin overview of database size and backup status. Shares the
// Error Log permission: both are operational views for the same admins.
[ApiController]
[Route("api/admin/database/overview")]
[Authorize(Policy = PermissionConstants.SystemViewErrorLog)]
public class DatabaseOverviewController : ControllerBase
{
    private readonly IDatabaseOverviewService _service;

    public DatabaseOverviewController(IDatabaseOverviewService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var overview = await _service.GetOverviewAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(overview));
    }
}
