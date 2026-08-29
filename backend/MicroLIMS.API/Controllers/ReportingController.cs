using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Read-only search/trend API over the ResultRecord flattened projection and
// dedicated reports (Media/GPT, Reference Strains). All authenticated
// roles can read; the only write in this controller is the export audit
// trail (see Export methods below).
[ApiController]
[Route("api/reporting")]
[Authorize]
public class ReportingController : ControllerBase
{
    // A too-broad export filter must be narrowed by the user, not
    // silently truncated - see ReportingQueryService.GetForExportAsync.
    private const int MaxExportRows = 10_000;

    private readonly ReportingQueryService _query;
    private readonly DataExportAuditService _exportAudit;
    private readonly MediaGptReportService _mediaGptReport;
    private readonly ReferenceStrainReportService _referenceStrainReport;

    public ReportingController(
        ReportingQueryService query,
        DataExportAuditService exportAudit,
        MediaGptReportService mediaGptReport,
        ReferenceStrainReportService referenceStrainReport)
    {
        _query = query;
        _exportAudit = exportAudit;
        _mediaGptReport = mediaGptReport;
        _referenceStrainReport = referenceStrainReport;
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults(
        [FromQuery] string? search,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? testCode,
        [FromQuery] ResultLevel? resultLevel,
        [FromQuery] SampleStatus? sampleStatus,
        [FromQuery] string? approvalStatus,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? subjectName,
        [FromQuery] ResultKind? resultKind,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sortBy = "ResultEnteredAt",
        [FromQuery] bool sortDescending = true)
    {
        var result = await _query.SearchAsync(new ResultRecordSearchRequest(
            search, category, testCode, resultLevel, sampleStatus, approvalStatus, fromDate, toDate,
            subjectName, resultKind, page, pageSize, sortBy, sortDescending));

        var items = result.Items.Select(r => new
        {
            r.Id, r.SampleId, r.TestOrderId, r.SourceTable, r.SourceId, r.Round,
            r.ReferenceNumber, r.Category, r.SubjectName, r.SubjectDetail, r.BatchNumber, r.ControlNumber,
            r.TestCode, r.TestDisplayName,
            r.ResultKind, r.NumericValue, r.ReportedValue, r.Unit, r.IsBelowDetectionLimit, r.DetectionLimit,
            r.AlertLimit, r.ActionLimit, r.SpecLimit, r.ResultLevel,
            r.ResultEnteredAt, r.ResultEnteredByUserId, r.ResultEnteredByName,
            r.SampleStatus, approvalStatus = ReportingQueryService.DeriveApprovalStatus(r.SampleStatus),
            r.ApprovedByUserId, r.ApprovedByName, r.ApprovedAt
        });

        return Ok(ApiResponse<object>.Ok(new { items, result.TotalCount, result.Page, result.PageSize }));
    }

    [HttpGet("results/{id}")]
    public async Task<IActionResult> GetById([FromRoute] int id)
    {
        var record = await _query.GetByIdAsync(id);
        if (record is null) return NotFound(ApiResponse<object>.Fail($"ResultRecord {id} not found."));

        return Ok(ApiResponse<object>.Ok(new
        {
            record.Id, record.SampleId, record.TestOrderId, record.SourceTable, record.SourceId, record.Round,
            record.ReferenceNumber, record.Category, record.SubjectName, record.SubjectDetail, record.BatchNumber, record.ControlNumber,
            record.TestCode, record.TestDisplayName,
            record.ResultKind, record.NumericValue, record.ReportedValue, record.Unit, record.IsBelowDetectionLimit, record.DetectionLimit,
            record.AlertLimit, record.ActionLimit, record.SpecLimit, record.ResultLevel,
            record.ResultEnteredAt, record.ResultEnteredByUserId, record.ResultEnteredByName,
            record.SampleStatus, approvalStatus = ReportingQueryService.DeriveApprovalStatus(record.SampleStatus),
            record.ApprovedByUserId, record.ApprovedByName, record.ApprovedAt
        }));
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate) =>
        Ok(ApiResponse<object>.Ok(await _query.GetOverviewAggregateAsync(fromDate, toDate)));

    [HttpGet("qualitative-events")]
    public async Task<IActionResult> GetQualitativeEvents(
        [FromQuery] string? testCode, [FromQuery] string? subjectName,
        [FromQuery] SampleCategory? category, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate) =>
        Ok(ApiResponse<object>.Ok(await _query.GetQualitativeEventsAsync(testCode, subjectName, category, fromDate, toDate)));

    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend([FromQuery] string testCode, [FromQuery] string subjectName, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate) =>
        Ok(ApiResponse<object>.Ok(await _query.GetTrendAsync(testCode, subjectName, fromDate, toDate)));

    [HttpGet("compare")]
    public async Task<IActionResult> GetCompare(
        [FromQuery] string testCode, [FromQuery] SampleCategory category,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate) =>
        Ok(ApiResponse<object>.Ok(await _query.GetCompareBySubjectAsync(testCode, category, fromDate, toDate)));

    [HttpGet("completed-by-month")]
    public async Task<IActionResult> GetCompletedByMonth([FromQuery] int months = 6) =>
        Ok(ApiResponse<object>.Ok(await _query.GetCompletedByMonthAsync(months)));

    // Distinct values actually present in ResultRecords - not master
    // data - so the filter panel's dropdowns never offer a choice that
    // comes back empty.
    [HttpGet("filter-options")]
    public async Task<IActionResult> GetFilterOptions() =>
        Ok(ApiResponse<object>.Ok(await _query.GetFilterOptionsAsync()));

    [HttpGet("results/export")]
    public async Task<IActionResult> ExportResults(
        [FromQuery] string? search,
        [FromQuery] SampleCategory? category,
        [FromQuery] string? testCode,
        [FromQuery] ResultLevel? resultLevel,
        [FromQuery] SampleStatus? sampleStatus,
        [FromQuery] string? approvalStatus,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? subjectName,
        [FromQuery] ResultKind? resultKind,
        [FromQuery] string sortBy = "ResultEnteredAt",
        [FromQuery] bool sortDescending = true)
    {
        var request = new ResultRecordSearchRequest(
            search, category, testCode, resultLevel, sampleStatus, approvalStatus, fromDate, toDate,
            subjectName, resultKind, SortBy: sortBy, SortDescending: sortDescending);

        // Throws InvalidOperationException (-> 400 via ExceptionMiddleware,
        // same pattern as GetTrendAsync's validation) when the filter
        // matches more than MaxExportRows - a too-broad export must be
        // narrowed, never silently truncated.
        var result = await _query.GetForExportAsync(request, MaxExportRows);
        if (result.Exceeded)
            throw new InvalidOperationException(
                $"This filter matches {result.TotalCount} rows, which exceeds the {MaxExportRows:N0}-row export limit. Narrow your filters (e.g. a shorter date range) and try again.");

        var csv = BuildCsv(result.Items);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var filterJson = JsonSerializer.Serialize(new
        {
            search, category, testCode, resultLevel, sampleStatus, approvalStatus,
            fromDate, toDate, subjectName, resultKind, sortBy, sortDescending
        });
        await _exportAudit.LogExportAsync(userId, filterJson, result.Items.Count, "ResultRecordsCsv");

        var fileName = $"microlims-results-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    // ==========================================
    // Media / GPT Report Endpoints
    // ==========================================

    [HttpGet("media-gpt")]
    public async Task<IActionResult> GetMediaGptResults(
        [FromQuery] string? search,
        [FromQuery] string? mediaType,
        [FromQuery] EvaluationType? evaluationType,
        [FromQuery] EvaluationOutcome? outcome,
        [FromQuery] ApprovalGateStatus? approvalStatus,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sortBy = "PreparedAt",
        [FromQuery] bool sortDescending = true)
    {
        var request = new MediaGptSearchRequest(
            search, mediaType, evaluationType, outcome, approvalStatus,
            fromDate, toDate, page, pageSize, sortBy, sortDescending);

        var result = await _mediaGptReport.SearchAsync(request);
        return Ok(ApiResponse<object>.Ok(new { items = result.Items, result.TotalCount, result.Page, result.PageSize }));
    }

    [HttpGet("media-gpt/{id}")]
    public async Task<IActionResult> GetMediaGptById([FromRoute] int id)
    {
        var detail = await _mediaGptReport.GetDetailAsync(id);
        if (detail is null) return NotFound(ApiResponse<object>.Fail($"Media lot {id} not found."));

        return Ok(ApiResponse<object>.Ok(detail));
    }

    [HttpGet("media-gpt/summary")]
    public async Task<IActionResult> GetMediaGptSummary(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? mediaType)
    {
        var summary = await _mediaGptReport.GetSummaryAsync(fromDate, toDate, mediaType);
        return Ok(ApiResponse<object>.Ok(summary));
    }

    [HttpGet("media-gpt/filter-options")]
    public async Task<IActionResult> GetMediaGptFilterOptions()
    {
        var options = await _mediaGptReport.GetFilterOptionsAsync();
        return Ok(ApiResponse<object>.Ok(options));
    }

    [HttpGet("media-gpt/export")]
    public async Task<IActionResult> ExportMediaGpt(
        [FromQuery] string? search,
        [FromQuery] string? mediaType,
        [FromQuery] EvaluationType? evaluationType,
        [FromQuery] EvaluationOutcome? outcome,
        [FromQuery] ApprovalGateStatus? approvalStatus,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string sortBy = "PreparedAt",
        [FromQuery] bool sortDescending = true)
    {
        var request = new MediaGptSearchRequest(
            search, mediaType, evaluationType, outcome, approvalStatus,
            fromDate, toDate, SortBy: sortBy, SortDescending: sortDescending);

        var result = await _mediaGptReport.GetForExportAsync(request, MaxExportRows);
        if (result.Exceeded)
            throw new InvalidOperationException(
                $"This filter matches {result.TotalCount} challenge rows, which exceeds the {MaxExportRows:N0}-row export limit. Narrow your filters (e.g. a shorter date range) and try again.");

        var csv = BuildMediaGptCsv(result.Items);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var filterJson = JsonSerializer.Serialize(new
        {
            search, mediaType, evaluationType, outcome, approvalStatus,
            fromDate, toDate, sortBy, sortDescending
        });
        await _exportAudit.LogExportAsync(userId, filterJson, result.Items.Count, "MediaGptCsv");

        var fileName = $"microlims-media-gpt-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    // ==========================================
    // Reference Strains Report Endpoints
    // ==========================================

    [HttpGet("reference-strains")]
    public async Task<IActionResult> GetReferenceStrainResults(
        [FromQuery] string? search,
        [FromQuery] int? organismId,
        [FromQuery] ApprovalGateStatus? approvalStatus,
        [FromQuery] bool? isDestroyed,
        [FromQuery] DateTime? receiptFromDate,
        [FromQuery] DateTime? receiptToDate,
        [FromQuery] DateTime? usageFromDate,
        [FromQuery] DateTime? usageToDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sortBy = "PreparedAt",
        [FromQuery] bool sortDescending = true)
    {
        var request = new ReferenceStrainSearchRequest(
            search, organismId, approvalStatus, isDestroyed,
            receiptFromDate, receiptToDate, usageFromDate, usageToDate,
            page, pageSize, sortBy, sortDescending);

        var result = await _referenceStrainReport.SearchAsync(request);
        return Ok(ApiResponse<object>.Ok(new { items = result.Items, result.TotalCount, result.Page, result.PageSize }));
    }

    [HttpGet("reference-strains/{id}")]
    public async Task<IActionResult> GetReferenceStrainById([FromRoute] int id)
    {
        var detail = await _referenceStrainReport.GetDetailAsync(id);
        if (detail is null) return NotFound(ApiResponse<object>.Fail($"Cryovial batch {id} not found."));

        return Ok(ApiResponse<object>.Ok(detail));
    }

    [HttpGet("reference-strains/filter-options")]
    public async Task<IActionResult> GetReferenceStrainFilterOptions()
    {
        var options = await _referenceStrainReport.GetFilterOptionsAsync();
        return Ok(ApiResponse<object>.Ok(options));
    }

    [HttpGet("reference-strains/export")]
    public async Task<IActionResult> ExportReferenceStrains(
        [FromQuery] string? search,
        [FromQuery] int? organismId,
        [FromQuery] ApprovalGateStatus? approvalStatus,
        [FromQuery] bool? isDestroyed,
        [FromQuery] DateTime? receiptFromDate,
        [FromQuery] DateTime? receiptToDate,
        [FromQuery] DateTime? usageFromDate,
        [FromQuery] DateTime? usageToDate,
        [FromQuery] string sortBy = "PreparedAt",
        [FromQuery] bool sortDescending = true)
    {
        var request = new ReferenceStrainSearchRequest(
            search, organismId, approvalStatus, isDestroyed,
            receiptFromDate, receiptToDate, usageFromDate, usageToDate,
            SortBy: sortBy, SortDescending: sortDescending);

        var result = await _referenceStrainReport.GetForExportAsync(request, MaxExportRows);
        if (result.Exceeded)
            throw new InvalidOperationException(
                $"This filter matches {result.TotalCount} rows, which exceeds the {MaxExportRows:N0}-row export limit. Narrow your filters and try again.");

        var csv = BuildReferenceStrainCsv(result.Items);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var filterJson = JsonSerializer.Serialize(new
        {
            search, organismId, approvalStatus, isDestroyed,
            receiptFromDate, receiptToDate, usageFromDate, usageToDate, sortBy, sortDescending
        });
        await _exportAudit.LogExportAsync(userId, filterJson, result.Items.Count, "ReferenceStrainsCsv");

        var fileName = $"microlims-reference-strains-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private static string BuildCsv(List<ResultRecord> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[]
        {
            "Date/Time", "Reference", "Subject", "Subject Detail", "Category", "Test Code", "Test Name",
            "Reported Value", "Unit", "Result Level", "Alert Limit", "Action Limit", "Spec Limit",
            "Sample Status", "Entered By", "Entered At", "Approved By", "Approved At", "Round"
        }.Select(EscapeCsvField)));

        foreach (var r in items)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                r.ResultEnteredAt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture),
                r.ReferenceNumber,
                r.SubjectName,
                r.SubjectDetail ?? "",
                r.Category.ToString(),
                r.TestCode,
                r.TestDisplayName,
                r.ReportedValue,
                r.Unit ?? "",
                r.ResultLevel.ToString(),
                r.AlertLimit ?? "",
                r.ActionLimit ?? "",
                r.SpecLimit ?? "",
                r.SampleStatus.ToString(),
                r.ResultEnteredByName,
                r.ResultEnteredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                r.ApprovedByName ?? "",
                r.ApprovedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "",
                r.Round.ToString(CultureInfo.InvariantCulture)
            }.Select(EscapeCsvField)));
        }

        return sb.ToString();
    }

    private static string BuildMediaGptCsv(List<MediaGptExportRowDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[]
        {
            "Lot Number", "Media Type", "Prepared Date", "Expiry Date", "Approval Status", "Released For Use",
            "Prepared By", "Approved By", "Approved At", "Evaluation Type", "Evaluation Status", "Evaluation Outcome",
            "Evaluation Completed At", "Organism", "ATCC Number", "Role", "Strain Source", "Initial Inoculum",
            "Reference Lot", "Old Count", "New Count", "Recovery %", "Expected Recovery Range", "Growth Observed",
            "Observed Description", "Expected Description", "Turbid", "Challenge Outcome", "Read By", "Read At"
        }.Select(EscapeCsvField)));

        foreach (var r in items)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                r.LotNumber,
                r.MediaType,
                r.PreparedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.ApprovalStatus,
                r.IsReleasedForUse ? "Yes" : "No",
                r.PreparedByName,
                r.ApprovedByName ?? "",
                r.ApprovedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                r.EvaluationType,
                r.EvaluationStatus,
                r.EvaluationOutcome ?? "",
                r.EvaluationCompletedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                r.OrganismName,
                r.AtccNumber ?? "",
                r.ChallengeRole ?? "",
                r.StrainSource ?? "",
                r.InitialInoculum,
                r.ReferenceMediaLot ?? "",
                r.OldMediaCount?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.NewMediaCount?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.RecoveryPercent?.ToString(CultureInfo.InvariantCulture) ?? "",
                r.ExpectedRecoveryRange ?? "",
                r.GrowthObserved.HasValue ? (r.GrowthObserved.Value ? "Yes" : "No") : "",
                r.ObservedDescription ?? "",
                r.ExpectedDescription ?? "",
                r.IsTurbid.HasValue ? (r.IsTurbid.Value ? "Yes" : "No") : "",
                r.ChallengeOutcome ?? "",
                r.ReadByName ?? "",
                r.ReadAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? ""
            }.Select(EscapeCsvField)));
        }

        return sb.ToString();
    }

    private static string BuildReferenceStrainCsv(List<ReferenceStrainExportRowDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', new[]
        {
            "Strain Name", "ATCC Number", "Cryovial Code", "Manufacturer", "Source Material",
            "Source Batch Number", "Receipt Date", "Prepared Date", "Expiry Date", "Vials Prepared",
            "Vials Remaining", "Storage Condition", "Approval Status", "Destroyed", "Prepared By",
            "Approved By", "Approved At", "Identity Confirmations Count", "Thaw Events Count",
            "Direct GPT Usage Count", "Indirect Test Orders Qualified"
        }.Select(EscapeCsvField)));

        foreach (var r in items)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                r.StrainName,
                r.AtccNumber ?? "",
                r.CryovialCode,
                r.ManufacturerName,
                r.SourceMaterialName,
                r.SourceMaterialBatchNumber,
                r.ReceiptDate > DateTime.MinValue ? r.ReceiptDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                r.PreparedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.NumberOfVialsPrepared.ToString(CultureInfo.InvariantCulture),
                r.VialsRemaining.ToString(CultureInfo.InvariantCulture),
                r.StorageCondition,
                r.ApprovalStatus,
                r.IsDestroyed ? "Yes" : "No",
                r.PreparedByName,
                r.ApprovedByName ?? "",
                r.ApprovedAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                r.IdentityConfirmationsCount.ToString(CultureInfo.InvariantCulture),
                r.ThawEventsCount.ToString(CultureInfo.InvariantCulture),
                r.DirectGptUsageCount.ToString(CultureInfo.InvariantCulture),
                r.IndirectTestOrdersCount.ToString(CultureInfo.InvariantCulture)
            }.Select(EscapeCsvField)));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
