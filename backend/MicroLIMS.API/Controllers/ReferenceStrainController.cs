using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public record IdentityConfirmationRowRequest(int MediaId, int IncubatorEquipmentId, DateTime IncubationStart, DateTime IncubationEnd, string ObservationText);
public record ReceiveStrainHttpRequest(string OrganismName, string AtccNumber, int NumberOfDiscs, string ManufacturerName, DateTime ExpiryDate, string StorageCondition, string PhysicalCheckText, List<IdentityConfirmationRowRequest> Panel);
public record PrepareCryovialsHttpRequest(int ReferenceStrainId, string ManufacturerName, DateTime ExpiryDate, int NumberOfVialsPrepared, string StorageCondition, string PhysicalCheckText, List<IdentityConfirmationRowRequest> Panel, int DiscsUsed);
public record ApproveRequest(bool Approved);

[ApiController]
[Route("api/reference-strains")]
[Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.Analyst + "," + RoleConstants.SystemAdministrator)]
public class ReferenceStrainController : ControllerBase
{
    private readonly ReferenceStrainService _service;

    public ReferenceStrainController(ReferenceStrainService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(ApiResponse<object>.Ok(await _service.GetAllAsync()));

    [HttpPost]
    public async Task<IActionResult> Receive(ReceiveStrainHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ReceiveAsync(new ReceiveStrainRequest(
            r.OrganismName, r.AtccNumber, r.NumberOfDiscs, r.ManufacturerName, r.ExpiryDate, r.StorageCondition, r.PhysicalCheckText,
            r.Panel.Select(p => new IdentityConfirmationRow(p.MediaId, p.IncubatorEquipmentId, p.IncubationStart, p.IncubationEnd, p.ObservationText)).ToList(),
            CurrentUserId))));

    [HttpPost("{id}/approve")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Approve(int id, ApproveRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ApproveAsync(id, r.Approved, CurrentUserId)));

    [HttpPost("cryovials")]
    public async Task<IActionResult> PrepareCryovials(PrepareCryovialsHttpRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.PrepareCryovialsAsync(new PrepareCryovialsRequest(
            r.ReferenceStrainId, r.ManufacturerName, r.ExpiryDate, r.NumberOfVialsPrepared, r.StorageCondition, r.PhysicalCheckText,
            r.Panel.Select(p => new IdentityConfirmationRow(p.MediaId, p.IncubatorEquipmentId, p.IncubationStart, p.IncubationEnd, p.ObservationText)).ToList(),
            r.DiscsUsed, CurrentUserId))));

    [HttpPost("cryovials/{id}/approve")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> ApproveCryovial(int id, ApproveRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ApproveCryovialAsync(id, r.Approved, CurrentUserId)));

    [HttpPost("cryovials/{id}/passage")]
    public async Task<IActionResult> RecordPassage(int id, [FromBody] string? notes) =>
        Ok(ApiResponse<object>.Ok(await _service.RecordPassageAsync(id, CurrentUserId, notes)));

    [HttpPost("cryovials/{id}/thaw")]
    public async Task<IActionResult> MarkThawed(int id)
    {
        await _service.MarkThawedAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("cryovials/{id}/destroy")]
    public async Task<IActionResult> Destroy(int id)
    {
        await _service.DestroyAsync(id);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
