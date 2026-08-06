using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// One-time (but safely repeatable) sweep that stands up the ResultRecord
// reporting projection against data that predates it - see
// ResultProjectionService.BackfillAsync. Administrative and potentially
// slow on a large dataset, so it is gated to SystemAdministrator only.
[ApiController]
[Route("api/admin/reporting")]
[Authorize(Roles = RoleConstants.SystemAdministrator)]
public class ReportingAdminController : ControllerBase
{
    private readonly ResultProjectionService _resultProjection;

    public ReportingAdminController(ResultProjectionService resultProjection)
    {
        _resultProjection = resultProjection;
    }

    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill() =>
        Ok(ApiResponse<object>.Ok(await _resultProjection.BackfillAsync()));
}
