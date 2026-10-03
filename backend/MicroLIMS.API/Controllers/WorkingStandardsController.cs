using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/working-standards")]
[Authorize]
public class WorkingStandardsController : ControllerBase
{
    private const long MaxUploadBytes = 30 * 1024 * 1024; // transport ceiling; service enforces 25 MB limit

    private readonly WorkingStandardService _service;

    public WorkingStandardsController(WorkingStandardService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ---- Lists & Reads ----

    [HttpGet("lots")]
    public async Task<IActionResult> GetLots() =>
        Ok(ApiResponse<object>.Ok(await _service.GetLotsAsync(CurrentUserId)));

    [HttpGet("qualifications")]
    public async Task<IActionResult> GetQualifications() =>
        Ok(ApiResponse<object>.Ok(await _service.GetQualificationsAsync(CurrentUserId)));

    [HttpGet("qualifications/{id:int}")]
    public async Task<IActionResult> Get(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAsync(id, CurrentUserId)));

    [HttpGet("source-samples")]
    public async Task<IActionResult> GetEligibleSourceSamples([FromQuery] string? search) =>
        Ok(ApiResponse<object>.Ok(await _service.GetEligibleSourceSamplesAsync(search, CurrentUserId)));

    // ---- Create / Edit ----

    [Authorize(Policy = PermissionConstants.WorkingStandardsQualify)]
    [HttpPost("qualifications")]
    public async Task<IActionResult> Create([FromBody] CreateQualificationRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.WorkingStandardsQualify)]
    [HttpPut("qualifications/{id:int}")]
    public async Task<IActionResult> UpdateDraft(int id, [FromBody] UpdateQualificationRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateDraftAsync(id, r, CurrentUserId)));

    // ---- Documents ----

    [Authorize(Policy = PermissionConstants.WorkingStandardsQualify)]
    [HttpPost("qualifications/{id:int}/documents")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> UploadDocument(int id, IFormFile file, [FromForm] WorkingStandardDocumentKind kind)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file was provided."));

        var content = await ReadAsync(file);
        return Ok(ApiResponse<object>.Ok(await _service.UploadDocumentAsync(
            id, kind, file.FileName, file.ContentType, content, CurrentUserId)));
    }

    [HttpGet("documents/{id:int}/file")]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var (content, contentType, fileName) = await _service.DownloadDocumentAsync(id, CurrentUserId);
        return File(content, contentType, fileName);
    }

    // ---- Sign-off ----

    [Authorize(Policy = PermissionConstants.SamplesReview)]
    [HttpPost("qualifications/{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] WorkingStandardSignRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ReviewAsync(id, r, CurrentUserId, ClientIpAddress)));

    [Authorize(Policy = PermissionConstants.SamplesReview)]
    [HttpPost("qualifications/{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] WorkingStandardReturnRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ReturnAsync(id, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.SamplesReview)]
    [HttpPost("qualifications/{id:int}/reject-review")]
    public async Task<IActionResult> RejectAtReview(int id, [FromBody] WorkingStandardReasonRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.RejectAtReviewAsync(id, r, CurrentUserId, ClientIpAddress)));

    [Authorize(Policy = PermissionConstants.SamplesApprove)]
    [HttpPost("qualifications/{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] WorkingStandardSignRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ApproveAsync(id, r, CurrentUserId, ClientIpAddress)));

    [Authorize(Policy = PermissionConstants.SamplesApprove)]
    [HttpPost("qualifications/{id:int}/reject-approval")]
    public async Task<IActionResult> RejectAtApproval(int id, [FromBody] WorkingStandardReasonRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.RejectAtApprovalAsync(id, r, CurrentUserId, ClientIpAddress)));

    private static async Task<byte[]> ReadAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }
}
