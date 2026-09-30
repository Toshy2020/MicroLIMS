using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Solution master for Mobile Phases, Diluents and Titrants (HPLC chain S2,
// spec 3.2). Reads are open to any authenticated user - the preparation
// area and picker need it; writes require MasterDataManage.
[ApiController]
[Route("api/masterdata/solution-masters")]
[Authorize]
public class SolutionMasterController : ControllerBase
{
    private readonly SolutionMasterService _service;
    public SolutionMasterController(SolutionMasterService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] SolutionType? type, [FromQuery] bool activeOnly = false) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(CurrentUserId, type, activeOnly)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSolutionMasterRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveSolutionMasterRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(id, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value, [FromBody] SetActiveRequest body) =>
        Ok(ApiResponse<object>.Ok(await _service.SetActiveAsync(id, value, body.Reason, CurrentUserId)));
}

public record SetActiveRequest(string Reason);
