using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/competency/qualifications")]
public class CompetencyController(
    QualificationService qualifications,
    CompetencySettingsService settings,
    CompetencyMatrixService matrix) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = PermissionConstants.CompetencyManage)]
    [RequestSizeLimit(DiscussionAttachmentPolicy.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DiscussionAttachmentPolicy.MaxRequestBytes)]
    public async Task<IActionResult> Qualify([FromForm] QualifyForm request, CancellationToken cancellationToken)
    {
        if (request.Attachments.Count > DiscussionAttachmentPolicy.MaxFiles ||
            request.Attachments.Any(f => f.Length > DiscussionAttachmentPolicy.MaxFileBytes))
            throw new BusinessRuleException(MessageCodes.CompetencyAttachmentLimit,
                $"Submit at most {DiscussionAttachmentPolicy.MaxFiles} attachments of up to {DiscussionAttachmentPolicy.MaxFileBytes / (1024 * 1024)} MB each.",
                new Dictionary<string, string>
                {
                    ["maxFiles"] = DiscussionAttachmentPolicy.MaxFiles.ToString(),
                    ["maxFileMb"] = (DiscussionAttachmentPolicy.MaxFileBytes / (1024 * 1024)).ToString()
                });

        var attachments = new List<QualificationFile>();
        foreach (var file in request.Attachments)
        {
            using var bytes = new MemoryStream();
            await file.CopyToAsync(bytes, cancellationToken);
            attachments.Add(new QualificationFile(file.FileName, bytes.ToArray()));
        }

        var qualification = await qualifications.QualifyAsync(
            new QualifyRequest(request.UserId, request.TestDefinitionId, request.Basis, request.Evidence,
                request.EvidenceSampleId, request.QualifiedOn, request.Password, attachments),
            CurrentUserId, cancellationToken);
        return Ok(ApiResponse<AnalystQualificationResponse>.Ok(qualification));
    }

    [HttpPost("{id:int}/withdraw")]
    [Authorize(Policy = PermissionConstants.CompetencyManage)]
    public async Task<IActionResult> Withdraw(int id, WithdrawRequest request, CancellationToken cancellationToken)
    {
        var qualification = await qualifications.WithdrawAsync(id, request.Reason, request.Password, CurrentUserId, cancellationToken);
        return Ok(ApiResponse<AnalystQualificationResponse>.Ok(qualification));
    }

    [HttpGet("~/api/competency/settings/{sectionId:int}")]
    [Authorize(Policy = PermissionConstants.CompetencyManage)]
    public async Task<IActionResult> GetSetting(int sectionId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CompetencySettingDto>.Ok(await settings.GetAsync(sectionId, CurrentUserId, cancellationToken)));

    [HttpGet("~/api/competency/settings/{sectionId:int}/impact")]
    [Authorize(Policy = PermissionConstants.CompetencyManage)]
    public async Task<IActionResult> GetImpact(int sectionId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<GateImpactDto>.Ok(await settings.GetImpactAsync(sectionId, CurrentUserId, cancellationToken)));

    [HttpPost("~/api/competency/settings/{sectionId:int}/gate")]
    [Authorize(Policy = PermissionConstants.CompetencyManage)]
    public async Task<IActionResult> SetGate(int sectionId, GateChangeRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CompetencySettingDto>.Ok(await settings.SetGateAsync(sectionId, request.Enabled,
            request.Reason, request.Password, request.ExpectedVersion, CurrentUserId, cancellationToken)));

    [HttpGet("~/api/competency/matrix/{sectionId:int}")]
    [Authorize(Policy = PermissionConstants.CompetencyView)]
    public async Task<IActionResult> GetMatrix(int sectionId, [FromQuery] MatrixFilters filters, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CompetencyMatrixDto>.Ok(await matrix.GetMatrixAsync(sectionId, filters, CurrentUserId, cancellationToken)));

    [HttpGet("~/api/competency/matrix/my")]
    [Authorize(Policy = PermissionConstants.CompetencyView)]
    public async Task<IActionResult> GetMy(CancellationToken cancellationToken) =>
        Ok(ApiResponse<CompetencyMatrixDto>.Ok(await matrix.GetMyAsync(CurrentUserId, cancellationToken)));

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public sealed class QualifyForm
    {
        public int UserId { get; init; }
        public int TestDefinitionId { get; init; }
        [BindRequired]
        public QualificationBasis Basis { get; init; }
        public string Evidence { get; init; } = string.Empty;
        public int? EvidenceSampleId { get; init; }
        public DateOnly QualifiedOn { get; init; }
        public string Password { get; init; } = string.Empty;
        public List<IFormFile> Attachments { get; init; } = new();
    }

    public sealed record WithdrawRequest(string Reason, string Password);

    public sealed record GateChangeRequest(bool Enabled, string Reason, string Password, uint? ExpectedVersion);
}
