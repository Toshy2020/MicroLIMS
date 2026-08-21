using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record SaveEquipmentInventoryHttpRequest(
    string InstrumentType,
    string ManufacturerName,
    string? SerialNumber,
    string? FirmwareVersion,
    string Code,
    string Location,
    DateTime? CalibrationDueDate,
    EquipmentOperationalStatus Status,
    string? StatusChangeComment = null);

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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _service.GetByIdAsync(id);
        if (item == null) return NotFound(ApiResponse<object>.Fail($"Equipment {id} not found."));
        return Ok(ApiResponse<object>.Ok(item));
    }

    [HttpGet("print")]
    public async Task<IActionResult> GetForPrint() => Ok(ApiResponse<object>.Ok(await _service.GetForPrintAsync()));

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveEquipment() =>
        Ok(ApiResponse<object>.Ok(await _service.GetActiveEquipmentAsync()));

    [HttpGet("{id:int}/activities")]
    public async Task<IActionResult> GetActiveActivities(int id)
    {
        try
        {
            return Ok(ApiResponse<object>.Ok(await _service.GetActiveActivitiesForEquipmentAsync(id)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id, [FromQuery] string? itemCode, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        try
        {
            return Ok(ApiResponse<object>.Ok(await _service.GetHistoricalActivitiesForEquipmentAsync(id, itemCode, fromDate, toDate)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("where-is-it")]
    public async Task<IActionResult> WhereIsIt([FromQuery] string query) =>
        Ok(ApiResponse<object>.Ok(await _service.WhereIsItAsync(query)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveEquipmentInventoryHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(new SaveEquipmentInventoryRequest(
            r.InstrumentType, r.ManufacturerName, r.SerialNumber, r.FirmwareVersion, r.Code, r.Location,
            r.CalibrationDueDate, r.Status), CurrentUserId)));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveEquipmentInventoryHttpRequest r)
    {
        try
        {
            await _service.UpdateAsync(id, new SaveEquipmentInventoryRequest(
                r.InstrumentType, r.ManufacturerName, r.SerialNumber, r.FirmwareVersion, r.Code, r.Location,
                r.CalibrationDueDate, r.Status, r.StatusChangeComment), CurrentUserId);
            return Ok(ApiResponse<object>.Ok(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{id:int}/status-history")]
    public async Task<IActionResult> GetStatusHistory(int id)
    {
        try
        {
            var history = await _service.GetStatusHistoryAsync(id);
            return Ok(ApiResponse<object>.Ok(history));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
