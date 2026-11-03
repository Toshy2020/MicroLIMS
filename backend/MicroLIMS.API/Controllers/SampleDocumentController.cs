using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Documents attached to a sample (the supplier's CoA). Upload needs
// SamplesReceiveOwnLab; void needs SamplesCorrect (as voiding the sample does:
// Section Head, Supervisor, Administrator); listing and content need only a signed-in user who can
// access one of the sample's laboratories (checked in the service).
[ApiController]
[Authorize]
public class SampleDocumentController : ControllerBase
{
    private readonly SampleDocumentService _service;

    public SampleDocumentController(SampleDocumentService service) => _service = service;

    private int CurrentUserId =>
        int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("api/samples/{sampleId:int}/documents")]
    public async Task<IActionResult> List(int sampleId, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _service.ListAsync(sampleId, CurrentUserId, ct)));

    [HttpPost("api/samples/{sampleId:int}/documents")]
    [Authorize(Policy = PermissionConstants.SamplesReceiveOwnLab)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Upload(int sampleId, IFormFile file, CancellationToken ct,
        [FromForm] SampleDocumentType documentType = SampleDocumentType.SupplierCoa)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file was provided."));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var result = await _service.UploadAsync(sampleId, file.FileName, file.ContentType, ms.ToArray(), documentType, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(result));
    }

    // The external lab's report for an outsourced test; its own route so the permission is an attribute.
    [HttpPost("api/test-workflow/{testOrderId:int}/external-report")]
    [Authorize(Policy = PermissionConstants.TestWorkflowExecute)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadExternalReport(int testOrderId, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file was provided."));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var result = await _service.UploadExternalReportAsync(testOrderId, file.FileName, file.ContentType, ms.ToArray(), CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(result));
    }

    // PDFs and images open inline in the browser; anything else downloads.
    [HttpGet("api/sample-documents/{documentId:int}/content")]
    public async Task<IActionResult> GetContent(int documentId, CancellationToken ct)
    {
        var (meta, content) = await _service.ReadContentAsync(documentId, CurrentUserId, ct);
        var inline = meta.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || meta.ContentType == "application/pdf";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!inline)
            return File(content, meta.ContentType, meta.OriginalFileName); // attachment, name encoded by MVC
        // ContentDisposition writes non-ASCII names as RFC 2231, which browsers decode back.
        Response.Headers["Content-Disposition"] =
            new System.Net.Mime.ContentDisposition { FileName = meta.OriginalFileName, Inline = true }.ToString();
        return File(content, meta.ContentType);
    }

    [HttpPost("api/sample-documents/{documentId:int}/void")]
    [Authorize(Policy = PermissionConstants.SamplesCorrect)]
    public async Task<IActionResult> Void(int documentId, VoidDocumentHttpRequest request, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _service.VoidAsync(documentId, request.Reason, CurrentUserId, ct)));
}
