using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Admin lifecycle of instrument agents. The plain key appears only in the
// register / rotate-key response and is never retrievable afterwards.
[ApiController]
[Route("api/instrument-agents")]
[Authorize(Policy = PermissionConstants.MasterDataManage)]
public class InstrumentAgentController : ControllerBase
{
    private readonly InstrumentAgentService _service;
    public InstrumentAgentController(InstrumentAgentService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterInstrumentAgentRequest r, CancellationToken ct)
    {
        var (agent, plainKey) = await _service.RegisterAsync(r.EquipmentId, r.Name, r.TimeZoneId, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new InstrumentAgentKeyResponse(agent, plainKey)));
    }

    [HttpPost("{id:int}/rotate-key")]
    public async Task<IActionResult> RotateKey(int id, CancellationToken ct)
    {
        var (agent, plainKey) = await _service.RotateKeyAsync(id, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new InstrumentAgentKeyResponse(agent, plainKey)));
    }

    [HttpPost("{id:int}/revoke")]
    public async Task<IActionResult> Revoke(int id, [FromBody] RevokeInstrumentAgentRequest r, CancellationToken ct)
    {
        await _service.RevokeAsync(id, r.Reason, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
