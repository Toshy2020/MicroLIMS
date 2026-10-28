using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Categories of the Other Materials group. Reads are open to any authenticated
// user (stock receivers need the picker); creating or changing one needs
// MaterialCategories.Manage plus a reason and an electronic signature.
[ApiController]
[Route("api/masterdata/material-categories")]
[Authorize]
public class MaterialCategoryController : ControllerBase
{
    private readonly MaterialCategoryService _service;
    public MaterialCategoryController(MaterialCategoryService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    // sectionId: only the categories available in that section.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false, [FromQuery] int? sectionId = null) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(activeOnly, sectionId)));

    [Authorize(Policy = PermissionConstants.MaterialCategoriesManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveMaterialCategoryRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(r, CurrentUserId, Ip)));

    [Authorize(Policy = PermissionConstants.MaterialCategoriesManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveMaterialCategoryRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(id, r, CurrentUserId, Ip)));

    [Authorize(Policy = PermissionConstants.MaterialCategoriesManage)]
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value) =>
        Ok(ApiResponse<object>.Ok(await _service.SetActiveAsync(id, value, CurrentUserId)));
}
