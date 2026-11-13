using System.Globalization;
using System.Security.Claims;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Labels;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroLIMS.API.Controllers;

public sealed record LabelStatusTargetRequest(LabelEntityType EntityType, int EntityId);

public sealed record LabelStatusRequest(IReadOnlyList<LabelStatusTargetRequest> Items);

[ApiController]
[Route("api/labels")]
[Authorize]
public class LabelController(
    ILabelPrintService printService,
    ILabelFormatService formatService) : ControllerBase
{
    private readonly ILabelPrintService _printService = printService ?? throw new ArgumentNullException(nameof(printService));
    private readonly ILabelFormatService _formatService = formatService ?? throw new ArgumentNullException(nameof(formatService));

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid)
            ? uid
            : throw new UnauthorizedAccessException("Authenticated user identity could not be determined.");

    private IReadOnlySet<string> CurrentPermissions =>
        User.FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

    [HttpPost("print")]
    public async Task<IActionResult> Print([FromBody] PrintLabelsRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _printService.PrintAsync(request, CurrentUserId, CurrentPermissions, ct);
            return File(result.Pdf, "application/pdf", result.FileName);
        }
        catch (LabelDoesNotFitException ex)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("status")]
    public async Task<IActionResult> Status([FromBody] LabelStatusRequest request, CancellationToken ct)
    {
        var records = request.Items
            .Select(i => (i.EntityType, i.EntityId))
            .ToList();
        var statuses = await _printService.GetStatusAsync(records, CurrentUserId, ct);
        return Ok(ApiResponse<IReadOnlyList<LabelPrintStatusDto>>.Ok(statuses));
    }

    [HttpGet("formats")]
    public async Task<IActionResult> GetFormats(CancellationToken ct)
    {
        var formats = await _formatService.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<LabelFormatDto>>.Ok(formats));
    }

    [Authorize(Policy = PermissionConstants.LabelsConfigure)]
    [HttpPut("formats/{id:int}")]
    public async Task<IActionResult> UpdateFormat(
        int id,
        [FromBody] UpdateLabelFormatRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) ||
            !uint.TryParse(ifMatch.Trim().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion))
        {
            return BadRequest(ApiResponse<object>.Fail("If-Match header with a valid record version is required."));
        }

        await _formatService.UpdateAsync(id, request, expectedVersion, ct);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
