using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Extensions;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Internet-facing endpoint the on-site edge agent posts instrument reports
// to. Callers authenticate with their per-agent key only (a human token is
// refused); the sender's identity comes from the key, never from the body.
[ApiController]
[Route("api/instrument-ingest")]
[Authorize(AuthenticationSchemes = AgentKeyDefaults.Scheme, Policy = AgentKeyDefaults.Policy)]
[EnableRateLimiting(RateLimitingPolicies.Agent)]
public class InstrumentIngestController : ControllerBase
{
    // Transport ceiling; the service enforces the 25 MB report and 2 MB manifest limits.
    public const long MaxRequestBytes = 30 * 1024 * 1024;
    private const int RetryAfterSeconds = 5;

    private readonly InstrumentIngestService _service;
    public InstrumentIngestController(InstrumentIngestService service) => _service = service;

    private int ClaimInt(string type) => int.Parse(User.FindFirst(type)!.Value, CultureInfo.InvariantCulture);

    // 200 for a new receipt and for a resend of the same bytes alike.
    [HttpPost("receive")]
    [RequestSizeLimit(MaxRequestBytes)]
    // No action parameters on purpose: any parameter makes MVC read the form
    // while binding, and a failure there becomes a 400 instead of Kestrel's 413.
    public async Task<IActionResult> Receive()
    {
        var ct = HttpContext.RequestAborted;
        try
        {
            var agent = await _service.ResolveAgentAsync(ClaimInt(AgentKeyDefaults.AgentIdClaim), ct);
            var receipt = await _service.ReceiveAsync(agent, await IngestForm.ReadAsync(Request, ct), ct);
            return Ok(ApiResponse<IngestReceipt>.Ok(receipt));
        }
        catch (IngestRetryableException)
        {
            // Nothing was saved and a resend is safe.
            Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail("Temporary conflict, retry"));
        }
    }

    // Lets the agent confirm an upload. Only this agent's equipment is searched.
    [HttpGet("receipt/{reportSha256}")]
    public async Task<IActionResult> GetReceipt(string reportSha256, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<IngestReceipt>>.Ok(
            await _service.GetReceiptsAsync(ClaimInt(AgentKeyDefaults.EquipmentIdClaim), reportSha256, ct)));
}

// Reads the multipart fields of POST receive by hand. Letting MVC bind the
// form would turn an over-limit body (Kestrel's 413) into a model-state 400,
// and a missing field into problem-details instead of the usual envelope.
internal static class IngestForm
{
    internal static async Task<IngestRequest> ReadAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType)
            throw new BusinessRuleException("The request must be multipart/form-data.");

        IFormCollection form;
        try { form = await request.ReadFormAsync(ct); }
        catch (InvalidDataException ex) { throw new BusinessRuleException("The form could not be read: " + ex.Message); }

        string Text(string name) => form[name].ToString();

        var report = form.Files.GetFile("report");
        if (report is null || report.Length == 0)
            throw new BusinessRuleException("The report file is required.");
        if (!int.TryParse(Text("injectionNumber"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var injection))
            throw new BusinessRuleException("The injection number is required.");
        if (!DateTime.TryParse(Text("agentClockUtc"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var agentClock))
            throw new BusinessRuleException("The agent clock must be an ISO-8601 UTC time.");

        using var ms = new MemoryStream();
        await report.CopyToAsync(ms, ct);
        var operatorName = Text("operatorAsReported");
        return new IngestRequest(
            Text("sequenceName"), injection, Text("acquiredAtLocalText"), Text("reportSha256"),
            report.FileName, ms.ToArray(), Text("manifest"), agentClock,
            string.IsNullOrWhiteSpace(operatorName) ? null : operatorName);
    }
}
