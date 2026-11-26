using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.Quality;

[ApiController]
[Route("api/quality")]
public sealed class QualityConfigController(QualityConfigService service, ILabClock clock) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0", CultureInfo.InvariantCulture);

    [HttpGet("config/templates/{type}")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> GetTemplate(QualityRecordType type, int? sectionId, CancellationToken ct = default) =>
        Result(await service.GetTemplateAsync(type, sectionId, Actor, ct));

    [HttpPut("config/templates/{type}")]
    [Authorize(Policy = PermissionPolicyProvider.QualityApproveOrInvestigate)]
    public async Task<IActionResult> SaveTemplate(QualityRecordType type, int? sectionId, SaveQualityTemplateRequest request,
        CancellationToken ct = default) => Result(await service.SaveTemplateAsync(type, sectionId, request, Version(), Actor, ct));

    [HttpGet("config/classifications")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ListClassifications(int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        Result(await service.ListClassificationsAsync(page, pageSize, Actor, ct));

    [HttpPost("config/classifications")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> CreateClassification(SaveQualityClassificationRequest request, CancellationToken ct = default) =>
        Result(await service.SaveClassificationAsync(0, request, Version(), Actor, ct));

    [HttpPut("config/classifications/{id:int:min(1)}")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> SaveClassification(int id, SaveQualityClassificationRequest request, CancellationToken ct = default) =>
        Result(await service.SaveClassificationAsync(id, request, Version(), Actor, ct));

    [HttpGet("config/due-rules")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ListDueRules(int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        Result(await service.ListDueRulesAsync(page, pageSize, Actor, ct));

    [HttpPut("config/due-rules/{type}")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> SaveDueRule(QualityRecordType type, SaveQualityDueRuleRequest request, CancellationToken ct = default) =>
        Result(await service.SaveDueRuleAsync(type, request.ClassificationId, request.WorkingDays, Version(), Actor, ct));

    [HttpGet("{recordId:int}/checklist-answers")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ListAnswers(int recordId, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        Result(await service.ListAnswersAsync(recordId, page, pageSize, Actor, ct));

    [HttpPost("{recordId:int}/checklist-answers/{itemKey}")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> Answer(int recordId, string itemKey, SaveQualityAnswerRequest request, CancellationToken ct = default) =>
        Result(await service.AnswerAsync(recordId, itemKey, request.Answer, request.Comment, Version(), Actor, ct));

    [HttpGet("{recordId:int}/extensions")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ListExtensions(int recordId, int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        Result(await service.ListExtensionsAsync(recordId, page, pageSize, Actor, ct));

    [HttpPost("{recordId:int}/extensions")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> RequestExtension(int recordId, RequestQualityExtensionRequest request, CancellationToken ct = default) =>
        Result(await service.RequestAsync(recordId, request.ActionId, request.RequestedDueDate, request.Reason, Version(), Actor, ct));

    [HttpPost("extensions/{requestId:int}/decision")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> DecideExtension(int requestId, DecideQualityExtensionRequest request, CancellationToken ct = default) =>
        Result(await service.DecideAsync(requestId, request.Approve, request.Comment, request.Password, Version(), Actor,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    [HttpGet("overdue")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> ListOverdue(int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        Result(await service.ListOverdueAsync(clock.LabToday, Actor, page, pageSize, ct));

    [HttpGet("section-settings/{sectionId:int}")]
    [Authorize(Policy = PermissionPolicyProvider.QualityApproveOrMasterDataManage)]
    public async Task<IActionResult> GetSectionSetting(int sectionId, CancellationToken ct = default) =>
        Result(await service.GetSectionSettingAsync(sectionId, Actor, ct));

    [HttpPut("section-settings/{sectionId:int}")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> SetSectionSetting(int sectionId, UpdateQualitySectionSettingRequest request,
        CancellationToken ct = default) =>
        Result(await service.SetSectionSettingAsync(sectionId, request.ChangeControlRequired, request.Reason,
            request.Password, RecordVersionMiddleware.ParseExpectedVersion(Request), Actor,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    private IActionResult Result<T>(T value) => Ok(ApiResponse<T>.Ok(value));

    private uint Version() => RecordVersionMiddleware.ParseExpectedVersion(Request)
        ?? throw new BusinessRuleException(MessageCodes.QualityVersionRequired, "An If-Match quality record version is required.");
}
