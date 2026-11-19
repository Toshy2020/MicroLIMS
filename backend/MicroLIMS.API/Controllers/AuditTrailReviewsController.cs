using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/audit-trail-reviews")]
[Authorize]
public class AuditTrailReviewsController : ControllerBase
{
    private const string ExportTypeName = "AuditTrailReviewPdf";

    private readonly IAuditTrailReviewService _service;
    private readonly IPdfGenerator _pdfGenerator;
    private readonly DataExportAuditService _exportAudit;

    public AuditTrailReviewsController(
        IAuditTrailReviewService service,
        IPdfGenerator pdfGenerator,
        DataExportAuditService exportAudit)
    {
        _service = service;
        _pdfGenerator = pdfGenerator;
        _exportAudit = exportAudit;
    }

    private int CurrentUserId
    {
        get
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
                throw new UnauthorizedAccessException("Authenticated user identifier is missing.");
            return userId;
        }
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    [Authorize(Policy = PermissionConstants.AuditView)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _service.ListAsync(page, pageSize, ct)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = PermissionConstants.AuditView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAsync(id, ct)));

    [HttpGet("{id:int}/entries")]
    [Authorize(Policy = PermissionConstants.AuditView)]
    public async Task<IActionResult> GetEntries(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _service.GetEntriesAsync(id, page, pageSize, ct)));

    [HttpGet("{id:int}/pdf")]
    [Authorize(Policy = PermissionConstants.AuditView)]
    public async Task<IActionResult> ExportPdf(int id, CancellationToken ct = default)
    {
        var review = await _service.GetAsync(id, ct);
        var document = AuditTrailReviewReportBuilder.Build(review);
        var pdfBytes = await _pdfGenerator.GenerateReportAsync(document);

        var filterJson = JsonSerializer.Serialize(new
        {
            reviewId = review.Id,
            scopeKind = review.ScopeKind.ToString(),
            scopeEntityName = review.ScopeEntityName,
            scopeRecordId = review.ScopeRecordId,
            cutoffAuditLogId = review.CutoffAuditLogId,
            status = review.Status.ToString()
        });
        await _exportAudit.LogExportAsync(CurrentUserId, filterJson, review.EntryCount, ExportTypeName);

        return File(pdfBytes, "application/pdf", $"audit-trail-review-{review.Id}.pdf");
    }

    [HttpPost]
    [Authorize(Policy = PermissionConstants.AuditReview)]
    public async Task<IActionResult> Start(
        [FromBody] StartAuditTrailReviewRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _service.StartAsync(request, CurrentUserId, ct)));

    [HttpPost("{id:int}/findings")]
    [Authorize(Policy = PermissionConstants.AuditReview)]
    public async Task<IActionResult> AddFinding(
        int id,
        [FromBody] AddAuditTrailReviewFindingRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _service.AddFindingAsync(id, request.AuditLogId, request.Note, CurrentUserId, ct)));

    [HttpPost("{id:int}/sign")]
    [Authorize(Policy = PermissionConstants.AuditReview)]
    public async Task<IActionResult> Sign(
        int id,
        [FromBody] SignAuditTrailReviewRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch = null,
        CancellationToken ct = default)
    {
        var rawVersion = !string.IsNullOrWhiteSpace(ifMatch)
            ? ifMatch
            : HttpContext?.Request.Headers.IfMatch.ToString();

        if (string.IsNullOrWhiteSpace(rawVersion) ||
            !uint.TryParse(rawVersion.Trim().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion))
        {
            return BadRequest(ApiResponse<object>.Fail("If-Match header containing a valid record version is required."));
        }

        var signed = await _service.SignAsync(id, request.Password, request.Conclusion, expectedVersion, CurrentUserId, ClientIp, ct);
        return Ok(ApiResponse<object>.Ok(signed));
    }

    [HttpPost("findings/{id:int}/close")]
    [Authorize(Policy = PermissionConstants.AuditReview)]
    public async Task<IActionResult> CloseFinding(
        int id,
        [FromBody] CloseAuditTrailReviewFindingRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _service.CloseFindingAsync(id, request.Comment, CurrentUserId, ct)));
}
