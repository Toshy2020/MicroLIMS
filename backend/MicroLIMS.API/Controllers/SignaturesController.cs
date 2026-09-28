using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// The signature trail for a record - what an auditor asks to see.
// Read-only: there is deliberately no POST/PUT/DELETE here or anywhere
// else for ElectronicSignature - see the entity's append-only comment.
[ApiController]
[Route("api/signatures")]
[Authorize(Policy = PermissionConstants.SignaturesManage)]
public class SignaturesController : ControllerBase
{
    private readonly ElectronicSignatureService _signatures;

    public SignaturesController(ElectronicSignatureService signatures)
    {
        _signatures = signatures;
    }

    [HttpGet]
    public async Task<IActionResult> GetTrail([FromQuery] string entityType, [FromQuery] int entityId) =>
        Ok(ApiResponse<object>.Ok(await _signatures.GetTrailAsync(entityType, entityId)));
}
