using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SaveEquipmentInventoryHttpRequest(
    string InstrumentType, string ManufacturerName, string? SerialNumber, string? FirmwareVersion,
    string Code, string Location, DateTime? CalibrationDueDate, EquipmentOperationalStatus Status);

// Inventory module - Equipment register (Microbiology lab only, per
// confirmed scope). Separate from /api/masterdata/equipment, which is
// the small workflow-linked list Media Preparation/GPT select from.
[ApiController]
[Route("api/inventory/equipment")]
[Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
public class EquipmentInventoryController : ControllerBase
{
    private readonly EquipmentInventoryService _service;

    public EquipmentInventoryController(EquipmentInventoryService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _service.GetAllAsync()));

    // Print/view list - excludes out-of-service and retired instruments.
    [HttpGet("print")]
    public async Task<IActionResult> GetForPrint() => Ok(ApiResponse<object>.Ok(await _service.GetForPrintAsync()));

    [HttpPost]
    public async Task<IActionResult> Create(SaveEquipmentInventoryHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(new SaveEquipmentInventoryRequest(
            r.InstrumentType, r.ManufacturerName, r.SerialNumber, r.FirmwareVersion, r.Code, r.Location,
            r.CalibrationDueDate, r.Status), CurrentUserId)));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, SaveEquipmentInventoryHttpRequest r)
    {
        await _service.UpdateAsync(id, new SaveEquipmentInventoryRequest(
            r.InstrumentType, r.ManufacturerName, r.SerialNumber, r.FirmwareVersion, r.Code, r.Location,
            r.CalibrationDueDate, r.Status), CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
