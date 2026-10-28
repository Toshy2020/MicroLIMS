using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Reagent / Indicator / Reference Standard master (HPLC chain S1, spec 3.1).
// Reads are open to any authenticated user and cover every lab - stock
// receivers need the picker; writes require MasterDataManage and the
// entry's owner lab.
[ApiController]
[Route("api/masterdata/material-masters")]
[Authorize]
public class MaterialMasterController : ControllerBase
{
    private readonly MaterialMasterService _service;
    public MaterialMasterController(MaterialMasterService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MaterialMasterCategory? category, [FromQuery] bool activeOnly = false) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(CurrentUserId, category, activeOnly)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveMaterialMasterEntryRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveMaterialMasterEntryRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(id, r, CurrentUserId)));

    // Reason + electronic signature; the only way to change the code once the entry has stock.
    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}/code")]
    public async Task<IActionResult> ChangeCode(int id, [FromBody] ChangeMaterialMasterEntryCodeRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ChangeCodeAsync(
            id, r.Code, r.Reason, r.Password, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value) =>
        Ok(ApiResponse<object>.Ok(await _service.SetActiveAsync(id, value, CurrentUserId)));
}
