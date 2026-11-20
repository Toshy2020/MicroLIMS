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
[Authorize(Policy = PermissionConstants.CompetencyManage)]
public class CompetencyController(QualificationService qualifications) : ControllerBase
{
    [HttpPost]
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
    public async Task<IActionResult> Withdraw(int id, WithdrawRequest request, CancellationToken cancellationToken)
    {
        var qualification = await qualifications.WithdrawAsync(id, request.Reason, request.Password, CurrentUserId, cancellationToken);
        return Ok(ApiResponse<AnalystQualificationResponse>.Ok(qualification));
    }

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
}
