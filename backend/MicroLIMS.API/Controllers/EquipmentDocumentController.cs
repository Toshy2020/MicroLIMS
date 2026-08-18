using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// API for managing controlled documents (Calibration Certificates) on Equipment.
// Role matrix:
//   - View / Download : Analyst, SectionHead, SystemAdministrator
//   - Upload          : Analyst, SectionHead, SystemAdministrator
//   - Supersede       : SectionHead, SystemAdministrator
//   - Void            : SectionHead, SystemAdministrator
//   - Delete          : Blocked for all roles (records preserved permanently)
[ApiController]
public class EquipmentDocumentController : ControllerBase
{
    private readonly EquipmentDocumentService _service;

    public EquipmentDocumentController(EquipmentDocumentService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // List all documents for an equipment record.
    [HttpGet("api/inventory/equipment/{equipmentId:int}/documents")]
    [Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> GetDocuments(int equipmentId)
    {
        try
        {
            var result = await _service.GetDocumentsAsync(equipmentId, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Upload a new document to an equipment record.
    [HttpPost("api/inventory/equipment/{equipmentId:int}/documents")]
    [Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [RequestSizeLimit(30 * 1024 * 1024)] // 30 MB server transport ceiling
    public async Task<IActionResult> Upload(int equipmentId, IFormFile file, [FromForm] EquipmentDocumentType documentType)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file was provided."));

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = ms.ToArray();
        }

        try
        {
            var result = await _service.UploadAsync(equipmentId, new UploadEquipmentDocumentRequest(
                documentType,
                file.FileName,
                file.ContentType,
                content), CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Retrieve the raw content of an equipment document.
    [HttpGet("api/inventory/equipment-documents/{documentId:int}/content")]
    [Authorize(Roles = RoleConstants.Analyst + "," + RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> GetContent(int documentId, [FromQuery] int equipmentId)
    {
        try
        {
            var (meta, bytes) = await _service.GetContentAsync(documentId, equipmentId, CurrentUserId);
            return File(bytes, meta.ContentType, meta.OriginalFileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Supersede an existing document with a replacement file.
    // SectionHead and SystemAdministrator only.
    [HttpPost("api/inventory/equipment-documents/{documentId:int}/supersede")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Supersede(int documentId, [FromQuery] int equipmentId, IFormFile file, [FromForm] string reason)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("A replacement file is required."));
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(ApiResponse<object>.Fail("A reason for supersession is required."));

        byte[] content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = ms.ToArray();
        }

        try
        {
            var result = await _service.SupersedeAsync(documentId, equipmentId, new SupersedeEquipmentDocumentRequest(
                file.FileName,
                file.ContentType,
                content,
                reason), CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Void a document. The file is retained for audit; document is marked Voided.
    // SectionHead and SystemAdministrator only.
    [HttpPost("api/inventory/equipment-documents/{documentId:int}/void")]
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    public async Task<IActionResult> Void(int documentId, [FromQuery] int equipmentId, [FromBody] VoidEquipmentDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(ApiResponse<object>.Fail("A reason for voiding is required."));

        try
        {
            var result = await _service.VoidAsync(documentId, equipmentId, request, CurrentUserId);
            return Ok(ApiResponse<object>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
