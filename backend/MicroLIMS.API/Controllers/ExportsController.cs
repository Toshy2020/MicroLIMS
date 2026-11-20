using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record LogListExportRequest(
    string TableKey,
    string FilterJson,
    IReadOnlyList<string> Columns,
    int RowCount,
    IReadOnlyList<string> RecordIds);

public sealed record LogListExportResponse(
    int ExportId,
    DateTime ServerTimeUtc,
    string TimeZoneId,
    string ExportedBy);

[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportsController : ControllerBase
{
    private const int MaxRequestBytes = 256 * 1024;

    private readonly DataExportAuditService _exportAudit;
    private readonly ILabClock _clock;

    public ExportsController(DataExportAuditService exportAudit, ILabClock clock)
    {
        _exportAudit = exportAudit;
        _clock = clock;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpPost("log")]
    [Authorize(Policy = PermissionConstants.DataExport)]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Log([FromBody] LogListExportRequest request, CancellationToken ct)
    {
        if (request.Columns is null || request.RecordIds is null)
            throw new BusinessRuleException(MessageCodes.ExportColumnsAndRecordIdsRequired, "Columns and recordIds are required.");

        if (request.RecordIds.Count != request.RowCount)
            throw new BusinessRuleException(
                MessageCodes.ExportRowCountMismatch,
                $"Export row count ({request.RowCount}) does not match the number of record identifiers ({request.RecordIds.Count}).",
                new Dictionary<string, string>
                {
                    ["recordIdCount"] = request.RecordIds.Count.ToString(CultureInfo.InvariantCulture),
                    ["rowCount"] = request.RowCount.ToString(CultureInfo.InvariantCulture)
                });

        JsonElement filtersElement;
        try
        {
            using var filterDoc = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(request.FilterJson) ? "{}" : request.FilterJson);
            filtersElement = filterDoc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BusinessRuleException(MessageCodes.ExportFilterJsonInvalid, "FilterJson must be valid JSON.");
        }

        var combinedFilterJson = JsonSerializer.Serialize(new
        {
            filters = filtersElement,
            columns = request.Columns,
            recordIds = request.RecordIds
        });

        var logged = await _exportAudit.LogListExportDtoAsync(
            CurrentUserId,
            request.TableKey,
            combinedFilterJson,
            request.RowCount,
            ct);

        return Ok(ApiResponse<LogListExportResponse>.Ok(new LogListExportResponse(
            logged.ExportId,
            logged.ServerTimeUtc,
            _clock.LabTimeZone.Id,
            logged.ExportedBy)));
    }
}
