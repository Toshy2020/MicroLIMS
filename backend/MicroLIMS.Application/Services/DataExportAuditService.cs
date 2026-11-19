using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.TableViews;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services;

// Writes DataExportLog rows - the only place anything in the Reports
// module is allowed to touch that table. Kept separate from the
// read-only ReportingQueryService so that service's "never writes"
// contract stays true.
public class DataExportAuditService
{
    public const int MaxListExportRows = 5000;

    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public DataExportAuditService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
    }

    public async Task LogExportAsync(int userId, string filterJson, int rowCount, string exportType)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        _db.DataExportLogs.Add(new DataExportLog
        {
            UserId = userId,
            UserName = user?.FullName ?? string.Empty,
            ExportedAt = _time.GetUtcNow().UtcDateTime,
            FilterJson = filterJson,
            RowCount = rowCount,
            ExportType = exportType
        });

        await _db.SaveChangesAsync();
    }

    public async Task<DataExportLog> LogListExportAsync(
        int userId,
        string tableKey,
        string filterJson,
        int rowCount,
        CancellationToken ct = default)
    {
        if (!TableKeyRegistry.IsKnown(tableKey))
            throw new BusinessRuleException(
                MessageCodes.ExportUnknownTableKey,
                $"Unknown table key '{tableKey}'.",
                new Dictionary<string, string> { ["tableKey"] = tableKey });

        if (rowCount < 0 || rowCount > MaxListExportRows)
            throw new BusinessRuleException(
                MessageCodes.ExportRowLimitExceeded,
                $"Export is limited to {MaxListExportRows:N0} rows. Narrow your filters or use the Reports module.",
                new Dictionary<string, string> { ["maxRows"] = MaxListExportRows.ToString("N0", CultureInfo.InvariantCulture) });

        ValidateFilterJsonRecordIds(filterJson, rowCount);

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            throw new BusinessRuleException(
                MessageCodes.ExportUserNotFound,
                "Exporting user could not be found.");

        var entry = new DataExportLog
        {
            UserId = userId,
            UserName = user.FullName,
            ExportedAt = _time.GetUtcNow().UtcDateTime,
            FilterJson = filterJson,
            RowCount = rowCount,
            ExportType = $"ListCsv:{tableKey}"
        };

        _db.DataExportLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<ListExportLogDto> LogListExportDtoAsync(
        int userId,
        string tableKey,
        string filterJson,
        int rowCount,
        CancellationToken ct = default)
    {
        var log = await LogListExportAsync(userId, tableKey, filterJson, rowCount, ct);
        return new ListExportLogDto(log.Id, log.ExportedAt, log.UserName);
    }

    private static void ValidateFilterJsonRecordIds(string filterJson, int rowCount)
    {
        if (string.IsNullOrWhiteSpace(filterJson))
            throw new BusinessRuleException(MessageCodes.ExportMetadataRequired, "Export metadata is required.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(filterJson);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException(MessageCodes.ExportMetadataInvalidJson, "Export metadata must be valid JSON.");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new BusinessRuleException(MessageCodes.ExportMetadataMustBeObject, "Export metadata must be a JSON object.");

            if (!doc.RootElement.TryGetProperty("recordIds", out var recordIdsProp)
                || recordIdsProp.ValueKind != JsonValueKind.Array)
            {
                throw new BusinessRuleException(MessageCodes.ExportMetadataRecordIdsRequired, "Export metadata must include recordIds.");
            }

            if (recordIdsProp.GetArrayLength() != rowCount)
                throw new BusinessRuleException(
                    MessageCodes.ExportRowCountMismatch,
                    $"Export row count ({rowCount}) does not match the number of record identifiers ({recordIdsProp.GetArrayLength()}).",
                    new Dictionary<string, string>
                    {
                        ["recordIdCount"] = recordIdsProp.GetArrayLength().ToString(CultureInfo.InvariantCulture),
                        ["rowCount"] = rowCount.ToString(CultureInfo.InvariantCulture),
                    });
        }
    }
}
