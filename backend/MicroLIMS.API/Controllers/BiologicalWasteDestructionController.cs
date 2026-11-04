using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Biological waste destruction records - one signed record per autoclave
// cycle, covering expired/withdrawn cryovial vials and anything else
// destroyed in the same cycle. See BiologicalWasteDestructionService.
[ApiController]
[Route("api/biological-waste-destructions")]
[Authorize(Policy = PermissionConstants.CryovialsManage)]
public class BiologicalWasteDestructionController : ControllerBase
{
    private readonly BiologicalWasteDestructionService _service;
    private readonly IUserSectionScopeService _scopeService;

    public BiologicalWasteDestructionController(BiologicalWasteDestructionService service, IUserSectionScopeService scopeService)
    {
        _service = service;
        _scopeService = scopeService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(await _scopeService.GetReadableSectionIdsAsync(CurrentUserId))));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, await _scopeService.GetReadableSectionIdsAsync(CurrentUserId))));

    // Cryovial material currently eligible for destruction.
    [HttpGet("eligible")]
    public async Task<IActionResult> GetEligible() =>
        Ok(ApiResponse<object>.Ok(await _service.GetEligibleAsync(await _scopeService.GetReadableSectionIdsAsync(CurrentUserId))));

    [HttpPost]
    public async Task<IActionResult> Record(RecordBiologicalWasteDestructionRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.RecordAsync(r, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));
}
