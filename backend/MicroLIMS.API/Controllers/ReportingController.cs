using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Read-only search/trend API over the ResultRecord flattened projection -
// the data layer foundation for the Reports module. All authenticated
// roles can read; the only write in this controller is the export audit
// trail (see Export below). Query logic itself lives in
// ReportingQueryService so it can be unit tested without a running host.
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

    public ReportingController(ReportingQueryService query, DataExportAuditService exportAudit)
    {
        _query = query;
        _exportAudit = exportAudit;
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

    private static string EscapeCsvField(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return field;
        return $"\"{field.Replace("\"", "\"\"")}\"";
    }
}
