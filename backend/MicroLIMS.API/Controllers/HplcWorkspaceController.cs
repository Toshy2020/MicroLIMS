using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// HPLC Workspace (HPLC chain S6, spec 5). Reads are open to any authenticated
// user (section-scoped in the service); every write requires Hplc.Operate.
[ApiController]
[Route("api/hplc-workspace")]
[Authorize]
public class HplcWorkspaceController : ControllerBase
{
    private const long MaxUploadBytes = 30 * 1024 * 1024; // transport ceiling; the service enforces the 25 MB file limit

    private readonly HplcRunService _service;
    private readonly ITestWorkflowEngine _engine;
    public HplcWorkspaceController(HplcRunService service, ITestWorkflowEngine engine)
    {
        _service = service;
        _engine = engine;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ---- Reads ----

    [HttpGet("instruments")]
    public async Task<IActionResult> GetInstruments() =>
        Ok(ApiResponse<object>.Ok(await _service.GetInstrumentsAsync(CurrentUserId)));

    [HttpGet("method-options")]
    public async Task<IActionResult> GetMethodOptions() =>
        Ok(ApiResponse<object>.Ok(await _service.GetMethodOptionsAsync(CurrentUserId)));

    [HttpGet("instruments/{equipmentId:int}/runs")]
    public async Task<IActionResult> GetRunHistory(int equipmentId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetRunHistoryAsync(equipmentId, CurrentUserId)));

    [HttpGet("runs/{id:int}")]
    public async Task<IActionResult> GetRun(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetRunAsync(id, CurrentUserId)));

    [HttpGet("runs/{id:int}/eligible-tests")]
    public async Task<IActionResult> GetEligibleTests(int id, [FromQuery] string? search) =>
        Ok(ApiResponse<object>.Ok(await _service.GetEligibleTestsAsync(id, search, CurrentUserId)));

    [HttpGet("samples/{runSampleId:int}")]
    public async Task<IActionResult> GetSampleEntry(int runSampleId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetSampleEntryAsync(runSampleId, CurrentUserId)));

    [HttpGet("evidence/{id:int}/file")]
    public async Task<IActionResult> DownloadEvidence(int id)
    {
        var (content, contentType, fileName) = await _service.DownloadEvidenceAsync(id, CurrentUserId);
        return File(content, contentType, fileName);
    }

    // ---- Runs / system suitability ----

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs")]
    public async Task<IActionResult> StartRun([FromBody] StartHplcRunRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.StartRunAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPut("runs/{id:int}/sst")]
    public async Task<IActionResult> SaveSst(int id, [FromBody] SaveSstRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.SaveSstAsync(id, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs/{id:int}/sst/confirm")]
    public async Task<IActionResult> ConfirmSst(int id, [FromBody] ConfirmSstRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.ConfirmSstAsync(id, r, CurrentUserId, ClientIpAddress)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs/{id:int}/abandon")]
    public async Task<IActionResult> Abandon(int id, [FromBody] ReasonRequest body) =>
        Ok(ApiResponse<object>.Ok(await _service.AbandonRunAsync(id, body.Reason, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.CompleteRunAsync(id, CurrentUserId)));

    // ---- Samples ----

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs/{id:int}/samples")]
    public async Task<IActionResult> AssignSamples(int id, [FromBody] AssignHplcSamplesRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.AssignSamplesAsync(id, r.TestOrderIds, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs/{id:int}/samples/{runSampleId:int}/remove")]
    public async Task<IActionResult> RemoveSample(int id, int runSampleId, [FromBody] ReasonRequest body) =>
        Ok(ApiResponse<object>.Ok(await _service.RemoveSampleAsync(id, runSampleId, body.Reason, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPut("samples/{runSampleId:int}/replicates")]
    public async Task<IActionResult> SaveReplicates(int runSampleId, [FromBody] SaveReplicatesRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.SaveReplicatesAsync(runSampleId, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("samples/{runSampleId:int}/submit")]
    public async Task<IActionResult> Submit(int runSampleId, [FromBody] SubmitHplcSampleRequest r) =>
        Ok(ApiResponse<object>.Ok(await _engine.SubmitHplcMethodAssayAsync(runSampleId, r.Password, r.Comment, CurrentUserId, ClientIpAddress)));

    // ---- Evidence ----

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs/{id:int}/evidence")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> UploadEvidence(
        int id, IFormFile file, [FromForm] HplcEvidenceContext context, [FromForm] HplcEvidenceKind kind, [FromForm] int? runSampleId)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file was provided."));

        var content = await ReadAsync(file);
        return Ok(ApiResponse<object>.Ok(await _service.UploadEvidenceAsync(
            id, runSampleId, context, kind, file.FileName, file.ContentType, content, CurrentUserId)));
    }

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("evidence/{id:int}/supersede")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> SupersedeEvidence(int id, IFormFile file, [FromForm] string reason)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("A replacement file is required."));

        var content = await ReadAsync(file);
        return Ok(ApiResponse<object>.Ok(await _service.SupersedeEvidenceAsync(
            id, reason, file.FileName, file.ContentType, content, CurrentUserId)));
    }

    private static async Task<byte[]> ReadAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return ms.ToArray();
    }
}
