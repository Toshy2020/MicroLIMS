using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/quality")]
public class QualityRecordsController(QualityRecordCore core, QualityLinkService links,
    QualityAttachmentService attachments, QualityCommentService comments) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0",
        System.Globalization.CultureInfo.InvariantCulture);

    [HttpGet("{id:int}")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct = default) =>
        Ok(ApiResponse<QualityRecordDto>.Ok(await core.ReadDtoAsync(id, Actor, ct)));

    [HttpPost("{id:int}/links")]
    [Authorize(Policy = PermissionConstants.QualityRaise)]
    public async Task<IActionResult> AddLink(int id, QualityLinkRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<QualityLinkDto>.Ok(await links.AddAsync(id, request.TargetType, request.TargetId, Actor, Version(), ct)));

    [HttpDelete("{id:int}/links/{linkId:int}")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> RemoveLink(int id, int linkId, CancellationToken ct = default)
    {
        await links.RemoveAsync(id, linkId, Actor, Version(), ct);
        return NoContent();
    }

    [HttpGet("{id:int}/links")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ResolveLinks(int id, int offset = 0, int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<QualityLinkDto>>.Ok(await links.ResolveAsync(id, Actor, offset, pageSize, ct)));

    [HttpGet("for-target/{type}/{targetId:int}")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ForTarget(QualityLinkTargetType type, int targetId, int offset = 0,
        int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<QualityRecordRefDto>>.Ok(await links.ForTargetAsync(type, targetId, Actor, offset, pageSize, ct)));

    [HttpPost("{id:int}/attachments")]
    [Authorize(Policy = PermissionConstants.QualityRaise)]
    [RequestSizeLimit(DiscussionAttachmentPolicy.MaxFileBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(int id, IFormFile file, CancellationToken ct = default)
    {
        var version = Version();
        if (file.Length is <= 0 or > DiscussionAttachmentPolicy.MaxFileBytes)
            throw QualityAttachmentService.InvalidAttachment();
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return Ok(ApiResponse<QualityAttachmentDto>.Ok(
            await attachments.UploadAsync(id, version, file.FileName, buffer.ToArray(), Actor, ct)));
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId, CancellationToken ct = default)
    {
        var content = await attachments.GetContentAsync(id, attachmentId, Actor, ct);
        Response.Headers["X-Quality-Attachment-Voided"] = content.IsVoided ? "true" : "false";
        return File(content.Bytes, content.ContentType, content.FileName);
    }

    [HttpPost("{id:int}/attachments/{attachmentId:int}/void")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> VoidAttachment(int id, int attachmentId, VoidQualityAttachmentRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<QualityAttachmentDto>.Ok(await attachments.VoidAsync(id, attachmentId, Version(), request.Reason, Actor, ct)));

    [HttpPost("{id:int}/comments")]
    [Authorize(Policy = PermissionConstants.QualityRaise)]
    public async Task<IActionResult> AddComment(int id, AddQualityCommentRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<QualityCommentDto>.Ok(await comments.AddAsync(id, Version(), request.Text, Actor, ct)));

    [HttpGet("{id:int}/comments")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ListComments(int id, int offset = 0, int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<IReadOnlyList<QualityCommentDto>>.Ok(await comments.ListAsync(id, Actor, offset, pageSize, ct)));

    private uint Version() => RecordVersionMiddleware.ParseExpectedVersion(Request)
        ?? throw new BusinessRuleException(MessageCodes.QualityVersionRequired, "An If-Match quality record version is required.");
}
