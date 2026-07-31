using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SaveMaterialHttpRequest(
    MaterialType MaterialType, string MaterialName, string ManufacturerName, string BatchNumber,
    DateTime ReceivingDate, DateTime? ExpiryDate, string? Code, string Location,
    decimal QuantityReceived, MaterialUnit Unit, decimal? MinimumStockLevel);

// Inventory module - Materials Stock. Day-to-day updates (receiving,
// stock count) are done by Analysts as well as Section Head/Admin,
// matching the access level of Reference Strain receiving.
[ApiController]
[Route("api/inventory/materials")]
[Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class MaterialController : ControllerBase
{
    private readonly MaterialService _service;

    public MaterialController(MaterialService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // ?type=DehydratedMedia lets Media Preparation's stock picker filter
    // down to just the usable dehydrated media containers.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MaterialType? type) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(type)));

    // Print/view list - excludes expired and depleted rows.
    [HttpGet("print")]
    public async Task<IActionResult> GetForPrint() => Ok(ApiResponse<object>.Ok(await _service.GetForPrintAsync()));

    [HttpGet("default-unit")]
    public IActionResult GetDefaultUnit([FromQuery] MaterialType materialType) =>
        Ok(ApiResponse<object>.Ok(new { unit = MaterialService.DefaultUnitFor(materialType) }));

    [HttpPost]
    public async Task<IActionResult> Create(SaveMaterialHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(new SaveMaterialRequest(
            r.MaterialType, r.MaterialName, r.ManufacturerName, r.BatchNumber, r.ReceivingDate, r.ExpiryDate,
            r.Code, r.Location, r.QuantityReceived, r.Unit, r.MinimumStockLevel), CurrentUserId)));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, SaveMaterialHttpRequest r)
    {
        await _service.UpdateAsync(id, new SaveMaterialRequest(
            r.MaterialType, r.MaterialName, r.ManufacturerName, r.BatchNumber, r.ReceivingDate, r.ExpiryDate,
            r.Code, r.Location, r.QuantityReceived, r.Unit, r.MinimumStockLevel), CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
