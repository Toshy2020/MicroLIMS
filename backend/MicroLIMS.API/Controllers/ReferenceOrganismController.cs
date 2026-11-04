using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Reference organism (strain) master. Reads are open to any authenticated
// user and cover every lab - stock receivers need the picker; writes require
// MasterDataManage and the strain's owner lab.
[ApiController]
[Route("api/masterdata/reference-organisms")]
[Authorize]
public class ReferenceOrganismController : ControllerBase
{
    private readonly ReferenceOrganismService _service;
    public ReferenceOrganismController(ReferenceOrganismService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false, [FromQuery] int? organismId = null) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(CurrentUserId, activeOnly, organismId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveReferenceOrganismRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveReferenceOrganismRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(id, r, CurrentUserId)));

    // Reason + electronic signature; the only way to change the code once the strain has stock.
    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}/code")]
    public async Task<IActionResult> ChangeCode(int id, [FromBody] ChangeReferenceOrganismCodeRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ChangeCodeAsync(
            id, r.Code, r.Reason, r.Password, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString())));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value) =>
        Ok(ApiResponse<object>.Ok(await _service.SetActiveAsync(id, value, CurrentUserId)));
}
