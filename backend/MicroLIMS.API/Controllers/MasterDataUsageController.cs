using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Read-only "where used" counts for master data records.
// TestDefinition, Specification and MaterialMaster usage requires MasterDataManage;
// Equipment and Item usage follows those records' own read policy ([Authorize],
// with section scope enforced in MasterDataUsageService).
[ApiController]
[Route("api/masterdata/usage")]
[Authorize]
public class MasterDataUsageController : ControllerBase
{
    private readonly MasterDataUsageService _service;
    private readonly IAuthorizationService _authorization;

    public MasterDataUsageController(MasterDataUsageService service, IAuthorizationService authorization)
    {
        _service = service;
        _authorization = authorization;
    }

    private int CurrentUserId =>
        int.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] MasterRecordType type,
        [FromQuery] int id,
        CancellationToken ct = default)
    {
        if (type is not (MasterRecordType.Equipment or MasterRecordType.Item))
        {
            var auth = await _authorization.AuthorizeAsync(User, PermissionConstants.MasterDataManage);
            if (!auth.Succeeded)
                return Forbid();
        }

        var summary = await _service.GetAsync(type, id, CurrentUserId, ct);
        return Ok(ApiResponse<UsageSummary>.Ok(summary));
    }
}
