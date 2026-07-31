using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record PrepareMediaHttpRequest(
    int MediaTypeId, int MaterialId, string ManufacturerLot, string ManufacturerName, decimal TotalWeight, string TotalVolume,
    int AutoclaveEquipmentId, string AutoclaveProgram, string LoadType, decimal Temperature,
    int CycleTime, int CycleNumber, decimal Ph, DateTime ExpiryDate);

// Media Preparation module - the autoclave/cycle/pH grid. Nothing here
// is usable in routine testing until it also passes GPT.
[ApiController]
[Route("api/media")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.Analyst + "," + RoleConstants.SystemAdministrator)]
public class MediaController : ControllerBase
{
    private readonly MediaPreparationService _mediaPrep;

    public MediaController(MediaPreparationService mediaPrep)
    {
        _mediaPrep = mediaPrep;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _mediaPrep.GetAllAsync()));

    [HttpGet("released")]
    public async Task<IActionResult> GetReleased([FromQuery] int? mediaTypeId) =>
        Ok(ApiResponse<object>.Ok(await _mediaPrep.GetReleasedAsync(mediaTypeId)));

    [HttpPost]
    public async Task<IActionResult> Prepare(PrepareMediaHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _mediaPrep.PrepareAsync(new PrepareMediaRequest(
            r.MediaTypeId, r.MaterialId, r.ManufacturerLot, r.ManufacturerName, r.TotalWeight, r.TotalVolume,
            r.AutoclaveEquipmentId, r.AutoclaveProgram, r.LoadType, r.Temperature,
            r.CycleTime, r.CycleNumber, r.Ph, r.ExpiryDate, CurrentUserId))));
}
